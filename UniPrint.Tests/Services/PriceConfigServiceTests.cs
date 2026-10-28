using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Tests.Services;

/// <summary>Task 20: Update Price Configuration (Admin).</summary>
public class PriceConfigServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly UniPrintDbContext _db;
    private readonly User _admin = new() { Email = "price-admin@test", FullName = "Admin", PasswordHash = "hash", Role = UserRole.Admin };
    private readonly User _staff = new() { Email = "price-staff@test", FullName = "Staff", PasswordHash = "hash", Role = UserRole.Staff };

    public PriceConfigServiceTests()
    {
        _connection.Open();
        _db = new UniPrintDbContext(new DbContextOptionsBuilder<UniPrintDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated(); // Seed: BW 350, Color 1500, giảm 10%, gáy 5000, ghim 1000
        _db.AddRange(_admin, _staff);
        _db.SaveChanges();
    }

    private PriceConfigService Service() => new(new UnitOfWork(_db));

    private static UpdatePriceConfigDto ValidRequest() => new()
    {
        PricePerBWPage = 400,
        PricePerColorPage = 2000,
        DoubleSidedDiscountPercent = 15,
        BindingPrice = 6000,
        StaplePrice = 1500
    };

    [Fact]
    public async Task GetActive_ReturnsSeededConfig()
    {
        var config = (await Service().GetActiveConfigAsync()).Data!;
        Assert.Equal(350m, config.PricePerBWPage);
        Assert.Equal(1500m, config.PricePerColorPage);
        Assert.True(config.IsActive);
    }

    [Fact]
    public async Task AdminUpdate_CreatesNewActiveVersion_AndKeepsHistory()
    {
        var result = await Service().UpdatePriceConfigAsync(_admin.Id, ValidRequest());

        Assert.True(result.Success);
        Assert.Equal(400m, result.Data!.PricePerBWPage);
        Assert.Equal(1, await _db.PriceConfigs.CountAsync(c => c.IsActive));
        Assert.Equal(2, await _db.PriceConfigs.CountAsync());

        var active = (await Service().GetActiveConfigAsync()).Data!;
        Assert.Equal(result.Data.Id, active.Id);
        Assert.Equal(15m, active.DoubleSidedDiscountPercent);

        var history = (await Service().GetHistoryAsync()).Data!;
        Assert.Equal(2, history.Count);
        Assert.True(history[0].IsActive);
        Assert.False(history[1].IsActive);
    }

    [Fact]
    public async Task NonAdmin_CannotUpdate()
    {
        var result = await Service().UpdatePriceConfigAsync(_staff.Id, ValidRequest());
        Assert.False(result.Success);
        Assert.Equal(1, await _db.PriceConfigs.CountAsync());
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        UpdatePriceConfigDto With(Action<UpdatePriceConfigDto> change)
        {
            var r = ValidRequest();
            change(r);
            return r;
        }
        yield return new object[] { With(r => r.PricePerBWPage = 0) };
        yield return new object[] { With(r => r.PricePerColorPage = -1) };
        yield return new object[] { With(r => r.PricePerColorPage = 300) }; // màu rẻ hơn đen trắng
        yield return new object[] { With(r => r.DoubleSidedDiscountPercent = 100) };
        yield return new object[] { With(r => r.DoubleSidedDiscountPercent = -5) };
        yield return new object[] { With(r => r.BindingPrice = -1) };
        yield return new object[] { With(r => r.StaplePrice = -1) };
        yield return new object[] { With(r => r.BindingPrice = 2_000_000) };
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidValues_AreRejected_WithoutChangingActiveConfig(UpdatePriceConfigDto request)
    {
        var result = await Service().UpdatePriceConfigAsync(_admin.Id, request);
        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors!);
        Assert.Equal(350m, (await Service().GetActiveConfigAsync()).Data!.PricePerBWPage);
    }

    [Fact]
    public async Task UnchangedValues_AreRejected()
    {
        var result = await Service().UpdatePriceConfigAsync(_admin.Id, new UpdatePriceConfigDto
        {
            PricePerBWPage = 350,
            PricePerColorPage = 1500,
            DoubleSidedDiscountPercent = 10,
            BindingPrice = 5000,
            StaplePrice = 1000
        });
        Assert.False(result.Success);
        Assert.Equal(1, await _db.PriceConfigs.CountAsync());
    }

    [Fact]
    public async Task NewPrice_AppliesToNewQuotes_ButExistingOrdersKeepLockedPrice()
    {
        var uow = new UnitOfWork(_db);
        var calculator = new PriceCalculatorService(uow);
        var orderService = new PrintOrderService(uow, calculator);
        var student = new User { Email = "price-student@test", FullName = "Student", PasswordHash = "hash" };
        var doc = new Document { User = student, FileName = "a.pdf", FileExtension = ".pdf", PageCount = 10 };
        _db.Add(doc);
        await _db.SaveChangesAsync();

        var oldOrder = await orderService.CreateOrderAsync(student.Id, new CreatePrintOrderDto { DocumentId = doc.Id });
        Assert.Equal(3500m, oldOrder.Data!.TotalPrice); // 350 x 10

        Assert.True((await Service().UpdatePriceConfigAsync(_admin.Id, ValidRequest())).Success);

        var quote = await calculator.CalculatePriceAsync(new CalculatePriceRequestDto { PageCount = 10 });
        Assert.Equal(4000m, quote.Data!.TotalEstimatedPrice); // 400 x 10

        var reloaded = await orderService.GetOrderByIdAsync(oldOrder.Data.Id);
        Assert.Equal(3500m, reloaded.Data!.TotalPrice); // BR02
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }
}
