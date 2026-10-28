using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Business.Services;

/// <summary>
/// Task 18 (Process Payment): Thanh toán tiền mặt lúc nhận hàng (Cash) hoặc quét mã chuyển khoản (VietQR).
/// - Sinh viên xem hóa đơn và chọn phương thức (Cash / VietQR) khi đơn chưa thanh toán.
/// - Staff xác nhận đã thu tiền mặt (khi giao tài liệu) hoặc đã nhận chuyển khoản (kèm mã giao dịch).
/// - Số tiền luôn lấy từ giá đã chốt trên đơn (BR02), không tính lại theo bảng giá hiện tại.
/// </summary>
public interface IPaymentService
{
    Task<ApiResponse<PaymentResponseDto>> GetPaymentByOrderAsync(Guid orderId, Guid userId, bool isStaffOrAdmin, CancellationToken ct = default);
    Task<ApiResponse<PaymentResponseDto>> SelectPaymentMethodAsync(Guid orderId, Guid studentId, PaymentMethod method, CancellationToken ct = default);
    Task<ApiResponse<PaymentResponseDto>> ConfirmPaymentAsync(Guid orderId, Guid staffId, ConfirmPaymentDto request, CancellationToken ct = default);
    Task<ApiResponse<List<PaymentResponseDto>>> GetUnpaidPaymentsAsync(string? keyword = null, CancellationToken ct = default);
}

public class PaymentService : IPaymentService
{
    private const int MaxTransactionCodeLength = 50;

    private static readonly PrintOrderStatus[] CashCollectableStatuses =
    {
        PrintOrderStatus.ShelfAssigned,
        PrintOrderStatus.ReadyForPickup,
        PrintOrderStatus.Completed
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly VietQrSettings _vietQrSettings;

    public PaymentService(IUnitOfWork unitOfWork, VietQrSettings vietQrSettings)
    {
        _unitOfWork = unitOfWork;
        _vietQrSettings = vietQrSettings;
    }

    public async Task<ApiResponse<PaymentResponseDto>> GetPaymentByOrderAsync(Guid orderId, Guid userId, bool isStaffOrAdmin, CancellationToken ct = default)
    {
        var order = await LoadOrderAsync(orderId, ct);
        // Không tiết lộ đơn của sinh viên khác.
        if (order == null || (!isStaffOrAdmin && order.StudentId != userId))
            return ApiResponse<PaymentResponseDto>.Fail("Không tìm thấy đơn in.");

        var payment = await EnsurePaymentAsync(order, ct);
        return ApiResponse<PaymentResponseDto>.Ok(PaymentMapper.ToDto(payment, order, _vietQrSettings));
    }

    public async Task<ApiResponse<PaymentResponseDto>> SelectPaymentMethodAsync(Guid orderId, Guid studentId, PaymentMethod method, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(method))
            return ApiResponse<PaymentResponseDto>.Fail("Phương thức thanh toán không hợp lệ.");

        var order = await LoadOrderAsync(orderId, ct);
        if (order == null || order.StudentId != studentId)
            return ApiResponse<PaymentResponseDto>.Fail("Không tìm thấy đơn in.");
        if (order.Status == PrintOrderStatus.Cancelled)
            return ApiResponse<PaymentResponseDto>.Fail("Đơn hàng đã bị hủy, không thể thanh toán.");

        var payment = await EnsurePaymentAsync(order, ct);
        if (payment.Status == PaymentStatus.Paid)
            return ApiResponse<PaymentResponseDto>.Fail("Đơn hàng đã được thanh toán, không thể đổi phương thức.");

        if (payment.Method != method)
        {
            payment.Method = method;
            payment.Amount = order.PrintOption.TotalPrice;
            _unitOfWork.Repository<Payment>().Update(payment);
            if (!await TrySaveAsync(ct))
                return ApiResponse<PaymentResponseDto>.Fail("Hóa đơn vừa được cập nhật. Vui lòng tải lại.");
        }

        var message = method == PaymentMethod.VietQR_PayOS
            ? "Quét mã VietQR bằng app ngân hàng để thanh toán. Nhân viên sẽ xác nhận khi nhận được tiền."
            : "Bạn sẽ thanh toán tiền mặt khi đến quầy nhận tài liệu.";
        return ApiResponse<PaymentResponseDto>.Ok(PaymentMapper.ToDto(payment, order, _vietQrSettings), message);
    }

    public async Task<ApiResponse<PaymentResponseDto>> ConfirmPaymentAsync(Guid orderId, Guid staffId, ConfirmPaymentDto request, CancellationToken ct = default)
    {
        var staff = await _unitOfWork.Repository<User>().GetByIdAsync(staffId, ct);
        if (staff == null || !staff.IsActive || (staff.Role != UserRole.Staff && staff.Role != UserRole.Admin))
            return ApiResponse<PaymentResponseDto>.Fail("Chỉ nhân viên được xác nhận thanh toán.");

        if (request == null || !Enum.IsDefined(request.Method))
            return ApiResponse<PaymentResponseDto>.Fail("Phương thức thanh toán không hợp lệ.");

        var order = await LoadOrderAsync(orderId, ct);
        if (order == null) return ApiResponse<PaymentResponseDto>.Fail("Không tìm thấy đơn in.");
        if (order.Status == PrintOrderStatus.Cancelled)
            return ApiResponse<PaymentResponseDto>.Fail("Đơn hàng đã bị hủy, không thể thanh toán.");

        var payment = await EnsurePaymentAsync(order, ct);
        if (payment.Status == PaymentStatus.Paid)
            return ApiResponse<PaymentResponseDto>.Fail("Đơn hàng đã được thanh toán trước đó.");

        string transactionCode;
        var now = DateTime.UtcNow;
        if (request.Method == PaymentMethod.Cash)
        {
            // Tiền mặt chỉ thu lúc sinh viên đến nhận tài liệu (đơn đã in xong / đã giao).
            if (!CashCollectableStatuses.Contains(order.Status))
                return ApiResponse<PaymentResponseDto>.Fail("Chỉ thu tiền mặt khi đơn đã in xong và sinh viên đến nhận tài liệu.");
            transactionCode = $"CASH-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
        }
        else
        {
            var code = request.TransactionCode?.Trim();
            if (string.IsNullOrEmpty(code))
                return ApiResponse<PaymentResponseDto>.Fail("Vui lòng nhập mã giao dịch ngân hàng để đối soát chuyển khoản.");
            if (code.Length > MaxTransactionCodeLength)
                return ApiResponse<PaymentResponseDto>.Fail($"Mã giao dịch tối đa {MaxTransactionCodeLength} ký tự.");
            transactionCode = code;
        }

        payment.Method = request.Method;
        payment.Amount = order.PrintOption.TotalPrice; // BR02: giá đã chốt trên đơn
        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = now;
        payment.TransactionCode = transactionCode;
        _unitOfWork.Repository<Payment>().Update(payment);

        if (!await TrySaveAsync(ct))
            return ApiResponse<PaymentResponseDto>.Fail("Hóa đơn vừa được cập nhật bởi người khác. Vui lòng tải lại trước khi thử tiếp.");

        return ApiResponse<PaymentResponseDto>.Ok(
            PaymentMapper.ToDto(payment, order, _vietQrSettings),
            request.Method == PaymentMethod.Cash
                ? "Đã xác nhận thu tiền mặt. Hóa đơn đã thanh toán."
                : "Đã xác nhận nhận chuyển khoản VietQR. Hóa đơn đã thanh toán.");
    }

    public async Task<ApiResponse<List<PaymentResponseDto>>> GetUnpaidPaymentsAsync(string? keyword = null, CancellationToken ct = default)
    {
        var query = _unitOfWork.Repository<Payment>().Query()
            .AsNoTracking()
            .Include(p => p.Order).ThenInclude(o => o.Student)
            .Include(p => p.Order).ThenInclude(o => o.PrintOption)
            .Where(p => p.Status != PaymentStatus.Paid && p.Order.Status != PrintOrderStatus.Cancelled);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(p => p.Order.OrderCode.Contains(kw) || p.Order.Student.FullName.Contains(kw));
        }

        var payments = await query.OrderBy(p => p.Order.CreatedAt).ToListAsync(ct);
        var result = payments.Select(p => PaymentMapper.ToDto(p, p.Order, _vietQrSettings)).ToList();
        return ApiResponse<List<PaymentResponseDto>>.Ok(result);
    }

    private Task<PrintOrder?> LoadOrderAsync(Guid orderId, CancellationToken ct)
        => _unitOfWork.Repository<PrintOrder>().Query()
            .Include(o => o.Student)
            .Include(o => o.PrintOption)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

    /// <summary>Đơn cũ (tạo trước khi có bảng Payment) được bổ sung hóa đơn tiền mặt theo giá đã chốt.</summary>
    private async Task<Payment> EnsurePaymentAsync(PrintOrder order, CancellationToken ct)
    {
        if (order.Payment != null) return order.Payment;

        var payment = new Payment
        {
            OrderId = order.Id,
            Order = order,
            Amount = order.PrintOption.TotalPrice,
            Method = PaymentMethod.Cash,
            Status = PaymentStatus.Pending
        };
        await _unitOfWork.Repository<Payment>().AddAsync(payment, ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Request khác vừa tạo hóa đơn cho đơn này (OrderId unique) -> dùng bản đã có.
            _unitOfWork.Repository<Payment>().Delete(payment);
            var existing = await _unitOfWork.Repository<Payment>().Query()
                .FirstAsync(p => p.OrderId == order.Id, ct);
            order.Payment = existing;
            return existing;
        }
        order.Payment = payment;
        return payment;
    }

    private async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}

/// <summary>Dùng chung cho Task 18 (hóa đơn) và Task 19 (chi tiết theo dõi đơn).</summary>
internal static class PaymentMapper
{
    public static PaymentResponseDto ToDto(Payment payment, PrintOrder order, VietQrSettings settings)
    {
        var dto = new PaymentResponseDto
        {
            Id = payment.Id,
            OrderId = order.Id,
            OrderCode = order.OrderCode,
            StudentName = order.Student?.FullName ?? string.Empty,
            OrderStatus = order.Status,
            Amount = payment.Amount,
            Method = payment.Method,
            Status = payment.Status,
            TransactionCode = payment.TransactionCode,
            PaidAt = payment.PaidAt,
            CreatedAt = payment.CreatedAt
        };

        if (payment.Method == PaymentMethod.VietQR_PayOS
            && payment.Status != PaymentStatus.Paid
            && order.Status != PrintOrderStatus.Cancelled)
        {
            var content = VietQrGenerator.BuildTransferContent(order.OrderCode);
            dto.VietQr = new VietQrInfoDto
            {
                BankBin = settings.BankBin,
                BankName = settings.BankName,
                AccountNo = settings.AccountNo,
                AccountName = settings.AccountName,
                Amount = VietQrGenerator.ToVndAmount(payment.Amount),
                TransferContent = content,
                QrPayload = VietQrGenerator.BuildPayload(settings.BankBin, settings.AccountNo, payment.Amount, content),
                QrImageUrl = VietQrGenerator.BuildImageUrl(settings, payment.Amount, content)
            };
        }

        return dto;
    }
}
