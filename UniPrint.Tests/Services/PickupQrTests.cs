using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using UniPrint.Business.Services;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Tests.Services;

public class PickupQrTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly UniPrintDbContext _db;
    private readonly User _staff = new() { Email = "pickup-staff@test", FullName = "Staff", PasswordHash = "hash", Role = UserRole.Staff };
    private readonly PrintOrder _order;
    private readonly PickupQR _qr;
    private readonly Shelf _shelf = new() { ShelfCode = "PICKUP", CurrentCount = 1 };

    public PickupQrTests()
    {
        _connection.Open();
        _db = NewContext();
        _db.Database.EnsureCreated();
        var student = new User { Email = "pickup-student@test", FullName = "Student", PasswordHash = "hash" };
        _order = new PrintOrder {
            OrderCode = "PICKUP-TEST", Student = student,
            Document = new Document { User = student, FileName = "pickup.pdf", FileExtension = ".pdf" },
            PrintOption = new PrintOption { NumberOfCopies = 1, TotalPrice = 1000 },
            Status = PrintOrderStatus.ReadyForPickup, Shelf = _shelf,
        };
        _qr = new PickupQR { Order = _order, QrToken = "pickup-token", ExpiresAt = DateTime.UtcNow.AddDays(7) };
        _db.AddRange(_staff, _qr);
        _db.SaveChanges();
    }

    private UniPrintDbContext NewContext() => new(new DbContextOptionsBuilder<UniPrintDbContext>().UseSqlite(_connection).Options);
    private static PrintOrderService Service(UniPrintDbContext db) => new(new UnitOfWork(db), Mock.Of<IPriceCalculatorService>());

    [Fact]
    public async Task ValidPickup_CompletesConsumesAndReleasesOnce()
    {
        var service = Service(_db);
        var result = await service.ScanPickupQrAsync("  pickup-token  ", _staff.Id);
        Assert.True(result.Success);
        Assert.Equal(PrintOrderStatus.Completed, result.Data!.Status);
        Assert.Equal(_order.Id, result.Data.Id);
        Assert.Null(result.Data.QrToken);
        Assert.True(_qr.IsUsed);
        Assert.NotNull(_qr.UsedAt);
        Assert.Equal(0, _shelf.CurrentCount);
        Assert.False((await service.ScanPickupQrAsync("pickup-token", _staff.Id)).Success);
        Assert.Equal(0, _shelf.CurrentCount);
    }

    [Theory]
    [InlineData(PrintOrderStatus.Pending)]
    [InlineData(PrintOrderStatus.Printing)]
    [InlineData(PrintOrderStatus.Cancelled)]
    [InlineData(PrintOrderStatus.Completed)]
    public async Task InvalidStatus_DoesNotMutate(PrintOrderStatus status)
    {
        _order.Status = status;
        await _db.SaveChangesAsync();
        Assert.False((await Service(_db).ScanPickupQrAsync(_qr.QrToken, _staff.Id)).Success);
        Assert.Equal(status, _order.Status);
        Assert.False(_qr.IsUsed);
        Assert.Equal(1, _shelf.CurrentCount);
    }

    [Fact]
    public async Task ExpiredPickup_PreservesReadyOrderAndShelf()
    {
        _qr.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await _db.SaveChangesAsync();
        var result = await Service(_db).ScanPickupQrAsync(_qr.QrToken, _staff.Id);
        Assert.False(result.Success);
        Assert.Contains("EC04", result.Message);
        Assert.Equal(PrintOrderStatus.ReadyForPickup, _order.Status);
        Assert.False(_qr.IsUsed);
        Assert.Equal(1, _shelf.CurrentCount);
    }

    [Fact]
    public async Task StudentCannotConsumeQr()
    {
        Assert.False((await Service(_db).ScanPickupQrAsync(_qr.QrToken, _order.StudentId)).Success);
        Assert.False(_qr.IsUsed);
        Assert.Equal(1, _shelf.CurrentCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    public async Task InvalidTokenDoesNotMutate(string token)
    {
        Assert.False((await Service(_db).ScanPickupQrAsync(token, _staff.Id)).Success);
        Assert.False(_qr.IsUsed);
        Assert.Equal(1, _shelf.CurrentCount);
    }

    [Fact]
    public async Task StaleScanRollsBackAllWrites()
    {
        using var stale = NewContext();
        await stale.PickupQRs.Include(q => q.Order).ThenInclude(o => o.Shelf).SingleAsync(q => q.Id == _qr.Id);
        Assert.True((await Service(_db).ScanPickupQrAsync(_qr.QrToken, _staff.Id)).Success);
        Assert.False((await Service(stale).ScanPickupQrAsync(_qr.QrToken, _staff.Id)).Success);
        using var check = NewContext();
        Assert.Equal(0, (await check.Shelves.SingleAsync(s => s.Id == _shelf.Id)).CurrentCount);
        Assert.True((await check.PickupQRs.SingleAsync(q => q.Id == _qr.Id)).IsUsed);
    }

    [Fact]
    public async Task CreateOrderGeneratesUniqueTokensWithSevenDayExpiry()
    {
        var calculator = new Mock<IPriceCalculatorService>();
        calculator.Setup(x => x.CalculatePriceAsync(It.IsAny<CalculatePriceRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<PriceCalculationResultDto>.Ok(new() { TotalEstimatedPrice = 1000, UnitPricePerPage = 1000 }));
        var service = new PrintOrderService(new UnitOfWork(_db), calculator.Object);
        var before = DateTime.UtcNow.AddDays(7);
        var request = new CreatePrintOrderDto { DocumentId = _order.DocumentId };
        var first = await service.CreateOrderAsync(_order.StudentId, request);
        var second = await service.CreateOrderAsync(_order.StudentId, request);
        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.NotEqual(first.Data!.QrToken, second.Data!.QrToken);
        Assert.InRange(first.Data.QrExpiresAt!.Value.UtcDateTime, before, DateTime.UtcNow.AddDays(7));
        var stored = await _db.PickupQRs.SingleAsync(q => q.OrderId == first.Data.Id);
        Assert.False(stored.IsUsed);
        Assert.Equal(stored.ExpiresAt, first.Data.QrExpiresAt.Value.UtcDateTime);
    }

    [Fact]
    public async Task ShelfConflictRollsBackOrderAndQrUpdates()
    {
        using var stale = NewContext();
        await stale.PickupQRs.Include(q => q.Order).ThenInclude(o => o.Shelf).SingleAsync(q => q.Id == _qr.Id);
        _shelf.CurrentCount = 2;
        await _db.SaveChangesAsync();
        Assert.False((await Service(stale).ScanPickupQrAsync(_qr.QrToken, _staff.Id)).Success);
        using var check = NewContext();
        Assert.Equal(2, (await check.Shelves.SingleAsync(s => s.Id == _shelf.Id)).CurrentCount);
        Assert.False((await check.PickupQRs.SingleAsync(q => q.Id == _qr.Id)).IsUsed);
        Assert.Equal(PrintOrderStatus.ReadyForPickup, (await check.PrintOrders.SingleAsync(o => o.Id == _order.Id)).Status);
    }

    [Fact]
    public async Task StudentOrdersExposeUtcExpiry()
    {
        var response = await Service(_db).GetStudentOrdersAsync(_order.StudentId);
        var dto = Assert.Single(response.Data!);
        Assert.Equal(TimeSpan.Zero, dto.QrExpiresAt!.Value.Offset);
        Assert.Equal(_qr.ExpiresAt, dto.QrExpiresAt.Value.UtcDateTime);
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }
}
