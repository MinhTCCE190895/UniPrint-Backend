using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Common;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Tests.Services;

/// <summary>Task 19: Get Order Tracking Details.</summary>
public class OrderTrackingTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly UniPrintDbContext _db;
    private readonly User _student = new() { Email = "track-student@test", FullName = "Track Student", PasswordHash = "hash" };
    private readonly User _otherStudent = new() { Email = "track-other@test", FullName = "Other", PasswordHash = "hash" };
    private readonly Shelf _shelf = new() { ShelfCode = "T1", Location = "Tầng 1 - Kệ Trái", CurrentCount = 1 };
    private readonly PrintOrder _order;
    private readonly PickupQR _qr;
    private readonly Payment _payment;

    public OrderTrackingTests()
    {
        _connection.Open();
        _db = new UniPrintDbContext(new DbContextOptionsBuilder<UniPrintDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _order = new PrintOrder
        {
            OrderCode = "ORD-20261028-TRACK1",
            Student = _student,
            Document = new Document { User = _student, FileName = "slide.pdf", FileExtension = ".pdf", PageCount = 20 },
            PrintOption = new PrintOption { NumberOfCopies = 2, IsColor = true, HasStaple = true, UnitPricePerPage = 1500, TotalPrice = 62000 },
            Status = PrintOrderStatus.ReadyForPickup,
            Shelf = _shelf,
            Notes = "Lấy buổi chiều"
        };
        _qr = new PickupQR { Order = _order, QrToken = "QR-track-token", ExpiresAt = DateTime.UtcNow.AddDays(3) };
        _payment = new Payment { Order = _order, Amount = 62000, Method = PaymentMethod.VietQR_PayOS, Status = PaymentStatus.Pending };
        _db.AddRange(_otherStudent, _qr, _payment);
        _db.SaveChanges();
    }

    private OrderTrackingService Service() => new(new UnitOfWork(_db), new VietQrSettings());

    [Fact]
    public async Task Owner_SeesFullTrackingDetails()
    {
        var result = await Service().GetOrderTrackingAsync(_order.Id, _student.Id, isStaffOrAdmin: false);

        Assert.True(result.Success);
        var t = result.Data!;
        Assert.Equal("ORD-20261028-TRACK1", t.OrderCode);
        Assert.Equal("slide.pdf", t.DocumentName);
        Assert.Equal(20, t.PageCount);
        Assert.Equal(2, t.NumberOfCopies);
        Assert.True(t.HasStaple);
        Assert.Equal(62000, t.TotalPrice);
        Assert.Equal("T1", t.ShelfCode);
        Assert.Equal("Tầng 1 - Kệ Trái", t.ShelfLocation);
        Assert.False(t.CanCancel);

        Assert.NotNull(t.PickupQr);
        Assert.True(t.PickupQr!.CanPickup);
        Assert.Equal("QR-track-token", t.PickupQr.QrToken);

        Assert.NotNull(t.Payment);
        Assert.Equal(PaymentMethod.VietQR_PayOS, t.Payment!.Method);
        Assert.NotNull(t.Payment.VietQr);
        Assert.Equal("ORD20261028TRACK1", t.Payment.VietQr!.TransferContent);

        Assert.Equal(5, t.Timeline.Count);
        var current = Assert.Single(t.Timeline, s => s.IsCurrent);
        Assert.Equal(PrintOrderStatus.ReadyForPickup, current.Status);
        Assert.Equal(4, t.Timeline.Count(s => s.IsDone));
    }

    [Fact]
    public async Task OtherStudent_GetsNotFound_StaffCanView()
    {
        Assert.False((await Service().GetOrderTrackingAsync(_order.Id, _otherStudent.Id, isStaffOrAdmin: false)).Success);
        Assert.True((await Service().GetOrderTrackingAsync(_order.Id, Guid.NewGuid(), isStaffOrAdmin: true)).Success);
        Assert.False((await Service().GetOrderTrackingAsync(Guid.NewGuid(), _student.Id, isStaffOrAdmin: false)).Success);
    }

    [Theory]
    [InlineData(PrintOrderStatus.Pending)]
    [InlineData(PrintOrderStatus.Printing)]
    [InlineData(PrintOrderStatus.Completed)]
    [InlineData(PrintOrderStatus.Cancelled)]
    public async Task QrToken_IsHiddenUnlessWaitingForPickup(PrintOrderStatus status)
    {
        _order.Status = status;
        await _db.SaveChangesAsync();
        var t = (await Service().GetOrderTrackingAsync(_order.Id, _student.Id, false)).Data!;
        Assert.False(t.PickupQr!.CanPickup);
        Assert.Null(t.PickupQr.QrToken);
    }

    [Fact]
    public async Task ExpiredOrUsedQr_IsHidden()
    {
        _qr.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await _db.SaveChangesAsync();
        var expired = (await Service().GetOrderTrackingAsync(_order.Id, _student.Id, false)).Data!;
        Assert.True(expired.PickupQr!.IsExpired);
        Assert.Null(expired.PickupQr.QrToken);

        _qr.ExpiresAt = DateTime.UtcNow.AddDays(1);
        _qr.IsUsed = true;
        _qr.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var used = (await Service().GetOrderTrackingAsync(_order.Id, _student.Id, false)).Data!;
        Assert.True(used.PickupQr!.IsUsed);
        Assert.NotNull(used.PickupQr.UsedAt);
        Assert.Null(used.PickupQr.QrToken);
    }

    [Fact]
    public async Task PaidInvoice_HidesVietQr()
    {
        _payment.Status = PaymentStatus.Paid;
        _payment.TransactionCode = "FT123";
        _payment.PaidAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var t = (await Service().GetOrderTrackingAsync(_order.Id, _student.Id, false)).Data!;
        Assert.Equal(PaymentStatus.Paid, t.Payment!.Status);
        Assert.Equal("FT123", t.Payment.TransactionCode);
        Assert.Null(t.Payment.VietQr);
    }

    [Fact]
    public async Task CancelledOrder_TimelineEndsWithCancelled()
    {
        _order.Status = PrintOrderStatus.Cancelled;
        await _db.SaveChangesAsync();
        var t = (await Service().GetOrderTrackingAsync(_order.Id, _student.Id, false)).Data!;
        Assert.Equal(2, t.Timeline.Count);
        Assert.Equal(PrintOrderStatus.Cancelled, t.Timeline.Last().Status);
        Assert.True(t.Timeline.Last().IsCurrent);
        Assert.Null(t.Payment!.VietQr);
    }

    [Fact]
    public async Task PendingOrder_CanCancel_AndShowsFirstStepCurrent()
    {
        _order.Status = PrintOrderStatus.Pending;
        await _db.SaveChangesAsync();
        var t = (await Service().GetOrderTrackingAsync(_order.Id, _student.Id, false)).Data!;
        Assert.True(t.CanCancel);
        Assert.True(t.Timeline[0].IsCurrent);
        Assert.Single(t.Timeline, s => s.IsDone);
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }
}
