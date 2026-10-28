using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Tests.Services;

/// <summary>Task 18: Process Payment (Cash khi nhận hàng / VietQR chuyển khoản).</summary>
public class PaymentServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly UniPrintDbContext _db;
    private readonly User _staff = new() { Email = "pay-staff@test", FullName = "Staff", PasswordHash = "hash", Role = UserRole.Staff };
    private readonly User _student = new() { Email = "pay-student@test", FullName = "Long Test Student", PasswordHash = "hash" };
    private readonly User _otherStudent = new() { Email = "pay-other@test", FullName = "Other", PasswordHash = "hash" };
    private readonly PrintOrder _order;
    private readonly Payment _payment;

    public PaymentServiceTests()
    {
        _connection.Open();
        _db = NewContext();
        _db.Database.EnsureCreated();
        _order = new PrintOrder
        {
            OrderCode = "ORD-20261028-ABC123",
            Student = _student,
            Document = new Document { User = _student, FileName = "baocao.pdf", FileExtension = ".pdf" },
            PrintOption = new PrintOption { NumberOfCopies = 1, TotalPrice = 27050m },
            Status = PrintOrderStatus.ReadyForPickup,
        };
        _payment = new Payment { Order = _order, Amount = 27050m, Method = PaymentMethod.Cash, Status = PaymentStatus.Pending };
        _db.AddRange(_staff, _otherStudent, _payment);
        _db.SaveChanges();
    }

    private UniPrintDbContext NewContext() => new(new DbContextOptionsBuilder<UniPrintDbContext>().UseSqlite(_connection).Options);
    private static PaymentService Service(UniPrintDbContext db) => new(new UnitOfWork(db), new VietQrSettings());

    [Fact]
    public async Task ConfirmCash_WhenReadyForPickup_MarksPaidWithLockedAmount()
    {
        _payment.Amount = 1; // Giá trên hóa đơn bị lệch -> phải lấy lại giá đã chốt trên đơn (BR02)
        await _db.SaveChangesAsync();

        var result = await Service(_db).ConfirmPaymentAsync(_order.Id, _staff.Id, new ConfirmPaymentDto { Method = PaymentMethod.Cash });

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Paid, result.Data!.Status);
        Assert.Equal(27050m, result.Data.Amount);
        Assert.StartsWith("CASH-", result.Data.TransactionCode);
        Assert.NotNull(result.Data.PaidAt);
        Assert.Null(result.Data.VietQr);
        using var check = NewContext();
        var stored = await check.Payments.SingleAsync(p => p.Id == _payment.Id);
        Assert.Equal(PaymentStatus.Paid, stored.Status);
        Assert.Equal(27050m, stored.Amount);
    }

    [Fact]
    public async Task ConfirmTwice_SecondAttemptFails()
    {
        var service = Service(_db);
        Assert.True((await service.ConfirmPaymentAsync(_order.Id, _staff.Id, new ConfirmPaymentDto { Method = PaymentMethod.Cash })).Success);
        var second = await service.ConfirmPaymentAsync(_order.Id, _staff.Id, new ConfirmPaymentDto { Method = PaymentMethod.Cash });
        Assert.False(second.Success);
        Assert.Contains("đã được thanh toán", second.Message);
    }

    [Fact]
    public async Task StaleConfirm_IsRejectedByConcurrencyToken()
    {
        using var stale = NewContext();
        await stale.PrintOrders.Include(o => o.Payment).Include(o => o.PrintOption).Include(o => o.Student).SingleAsync(o => o.Id == _order.Id);
        Assert.True((await Service(_db).ConfirmPaymentAsync(_order.Id, _staff.Id, new ConfirmPaymentDto { Method = PaymentMethod.Cash })).Success);

        var staleResult = await Service(stale).ConfirmPaymentAsync(_order.Id, _staff.Id,
            new ConfirmPaymentDto { Method = PaymentMethod.VietQR_PayOS, TransactionCode = "FT123" });

        Assert.False(staleResult.Success);
        using var check = NewContext();
        Assert.Equal(PaymentMethod.Cash, (await check.Payments.SingleAsync(p => p.Id == _payment.Id)).Method);
    }

    [Theory]
    [InlineData(PrintOrderStatus.Pending)]
    [InlineData(PrintOrderStatus.Processing)]
    [InlineData(PrintOrderStatus.Printing)]
    public async Task ConfirmCash_BeforePrintingDone_IsRejected(PrintOrderStatus status)
    {
        _order.Status = status;
        await _db.SaveChangesAsync();
        var result = await Service(_db).ConfirmPaymentAsync(_order.Id, _staff.Id, new ConfirmPaymentDto { Method = PaymentMethod.Cash });
        Assert.False(result.Success);
        Assert.Equal(PaymentStatus.Pending, _payment.Status);
    }

    [Fact]
    public async Task ConfirmVietQr_RequiresTransactionCode_AndAllowsBeforePickup()
    {
        _order.Status = PrintOrderStatus.Pending;
        await _db.SaveChangesAsync();
        var service = Service(_db);

        var missing = await service.ConfirmPaymentAsync(_order.Id, _staff.Id, new ConfirmPaymentDto { Method = PaymentMethod.VietQR_PayOS, TransactionCode = "  " });
        Assert.False(missing.Success);

        var ok = await service.ConfirmPaymentAsync(_order.Id, _staff.Id, new ConfirmPaymentDto { Method = PaymentMethod.VietQR_PayOS, TransactionCode = " FT26301123456 " });
        Assert.True(ok.Success);
        Assert.Equal("FT26301123456", ok.Data!.TransactionCode);
        Assert.Equal(PaymentMethod.VietQR_PayOS, ok.Data.Method);
    }

    [Fact]
    public async Task CancelledOrder_CannotBePaid()
    {
        _order.Status = PrintOrderStatus.Cancelled;
        await _db.SaveChangesAsync();
        Assert.False((await Service(_db).ConfirmPaymentAsync(_order.Id, _staff.Id,
            new ConfirmPaymentDto { Method = PaymentMethod.VietQR_PayOS, TransactionCode = "FT1" })).Success);
        Assert.False((await Service(_db).SelectPaymentMethodAsync(_order.Id, _student.Id, PaymentMethod.VietQR_PayOS)).Success);
    }

    [Fact]
    public async Task StudentCannotConfirmPayment()
    {
        var result = await Service(_db).ConfirmPaymentAsync(_order.Id, _student.Id, new ConfirmPaymentDto { Method = PaymentMethod.Cash });
        Assert.False(result.Success);
        Assert.Equal(PaymentStatus.Pending, _payment.Status);
    }

    [Fact]
    public async Task SelectVietQr_ReturnsQrWithLockedAmountAndOrderCode()
    {
        var result = await Service(_db).SelectPaymentMethodAsync(_order.Id, _student.Id, PaymentMethod.VietQR_PayOS);

        Assert.True(result.Success);
        Assert.Equal(PaymentMethod.VietQR_PayOS, result.Data!.Method);
        var qr = Assert.IsType<VietQrInfoDto>(result.Data.VietQr);
        Assert.Equal(27050, qr.Amount);
        Assert.Equal("ORD20261028ABC123", qr.TransferContent);
        Assert.Contains("540527050", qr.QrPayload);
        Assert.Contains("ORD20261028ABC123", qr.QrPayload);
        Assert.Contains("amount=27050", qr.QrImageUrl);
    }

    [Fact]
    public async Task SelectMethod_AfterPaid_IsRejected()
    {
        await Service(_db).ConfirmPaymentAsync(_order.Id, _staff.Id, new ConfirmPaymentDto { Method = PaymentMethod.Cash });
        var result = await Service(_db).SelectPaymentMethodAsync(_order.Id, _student.Id, PaymentMethod.VietQR_PayOS);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task OtherStudent_CannotSeeOrChangePayment()
    {
        Assert.False((await Service(_db).GetPaymentByOrderAsync(_order.Id, _otherStudent.Id, isStaffOrAdmin: false)).Success);
        Assert.False((await Service(_db).SelectPaymentMethodAsync(_order.Id, _otherStudent.Id, PaymentMethod.VietQR_PayOS)).Success);
        Assert.True((await Service(_db).GetPaymentByOrderAsync(_order.Id, _staff.Id, isStaffOrAdmin: true)).Success);
    }

    [Fact]
    public async Task InvalidMethodValue_IsRejected()
    {
        var result = await Service(_db).SelectPaymentMethodAsync(_order.Id, _student.Id, (PaymentMethod)0);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task LegacyOrderWithoutPayment_GetsCashInvoiceCreated()
    {
        var legacy = new PrintOrder
        {
            OrderCode = "ORD-LEGACY",
            Student = _student,
            Document = new Document { User = _student, FileName = "old.pdf", FileExtension = ".pdf" },
            PrintOption = new PrintOption { NumberOfCopies = 1, TotalPrice = 5000m },
            Status = PrintOrderStatus.ReadyForPickup,
        };
        _db.Add(legacy);
        await _db.SaveChangesAsync();

        var result = await Service(_db).GetPaymentByOrderAsync(legacy.Id, _student.Id, isStaffOrAdmin: false);

        Assert.True(result.Success);
        Assert.Equal(5000m, result.Data!.Amount);
        Assert.Equal(PaymentMethod.Cash, result.Data.Method);
        Assert.Equal(1, await _db.Payments.CountAsync(p => p.OrderId == legacy.Id));
    }

    [Fact]
    public async Task UnpaidList_ExcludesPaidAndCancelled_AndFiltersByKeyword()
    {
        // Seed data đã có sẵn 2 đơn mẫu chưa thanh toán.
        var service = Service(_db);
        Assert.Contains((await service.GetUnpaidPaymentsAsync()).Data!, p => p.OrderId == _order.Id);
        Assert.Single((await service.GetUnpaidPaymentsAsync("ABC123")).Data!);
        Assert.Single((await service.GetUnpaidPaymentsAsync("Long Test Student")).Data!);
        Assert.Empty((await service.GetUnpaidPaymentsAsync("KHONG-CO")).Data!);

        await service.ConfirmPaymentAsync(_order.Id, _staff.Id, new ConfirmPaymentDto { Method = PaymentMethod.Cash });
        Assert.DoesNotContain((await service.GetUnpaidPaymentsAsync()).Data!, p => p.OrderId == _order.Id);

        _db.PrintOrders.Single(o => o.Id == Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01")).Status = PrintOrderStatus.Cancelled;
        await _db.SaveChangesAsync();
        Assert.DoesNotContain((await service.GetUnpaidPaymentsAsync()).Data!,
            p => p.OrderId == Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
    }

    [Fact]
    public async Task PaidOrder_CannotBeCancelledByStudent()
    {
        _order.Status = PrintOrderStatus.Pending;
        await _db.SaveChangesAsync();
        Assert.True((await Service(_db).ConfirmPaymentAsync(_order.Id, _staff.Id,
            new ConfirmPaymentDto { Method = PaymentMethod.VietQR_PayOS, TransactionCode = "FT999" })).Success);

        var orderService = new PrintOrderService(new UnitOfWork(_db), new PriceCalculatorService(new UnitOfWork(_db)));
        var cancel = await orderService.CancelOrderAsync(_order.Id, _student.Id);

        Assert.False(cancel.Success);
        Assert.Equal(PrintOrderStatus.Pending, _order.Status);
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }
}

public class VietQrGeneratorTests
{
    [Fact]
    public void Crc16_MatchesCcittFalseCheckValue()
    {
        Assert.Equal(0x29B1, VietQrGenerator.Crc16Ccitt("123456789"));
    }

    [Fact]
    public void Payload_HasNapasStructureAndValidCrc()
    {
        var payload = VietQrGenerator.BuildPayload("970436", "1234567890", 27050.4m, "ORD20261028ABC123");

        Assert.StartsWith("000201010212", payload);
        Assert.Contains("0010A000000727", payload);
        Assert.Contains("0006970436", payload);
        Assert.Contains("01101234567890", payload);
        Assert.Contains("0208QRIBFTTA", payload);
        Assert.Contains("5303704", payload);
        Assert.Contains("540527050", payload);
        Assert.Contains("5802VN", payload);
        Assert.Contains("62210817ORD20261028ABC123", payload);

        var body = payload[..^4];
        Assert.EndsWith("6304", body);
        Assert.Equal(VietQrGenerator.Crc16Ccitt(body).ToString("X4"), payload[^4..]);
    }

    [Theory]
    [InlineData("ORD-20261028-abc123", "ORD20261028ABC123")]
    [InlineData("Đơn #1", "N1")]
    public void TransferContent_IsAsciiAlphanumericUppercase(string orderCode, string expected)
    {
        Assert.Equal(expected, VietQrGenerator.BuildTransferContent(orderCode));
    }
}
