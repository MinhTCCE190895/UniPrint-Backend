using UniPrint.DataAccess.Enums;

namespace UniPrint.Business.DTOs;

/// <summary>Task 18: Hóa đơn thanh toán của một đơn in.</summary>
public class PaymentResponseDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public PrintOrderStatus OrderStatus { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string MethodText => Method == PaymentMethod.VietQR_PayOS ? "Chuyển khoản VietQR" : "Tiền mặt";
    public PaymentStatus Status { get; set; }
    public string StatusText => Status switch
    {
        PaymentStatus.Paid => "Đã thanh toán",
        PaymentStatus.Failed => "Thanh toán lỗi",
        PaymentStatus.Refunded => "Đã hoàn tiền",
        _ => "Chưa thanh toán"
    };
    public string? TransactionCode { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Chỉ có khi phương thức là VietQR và hóa đơn chưa thanh toán.</summary>
    public VietQrInfoDto? VietQr { get; set; }
}

public class VietQrInfoDto
{
    public string BankBin { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string TransferContent { get; set; } = string.Empty;
    /// <summary>Chuỗi EMVCo để app tự vẽ QR.</summary>
    public string QrPayload { get; set; } = string.Empty;
    /// <summary>Ảnh QR dựng sẵn (img.vietqr.io) cho trang web.</summary>
    public string QrImageUrl { get; set; } = string.Empty;
}

/// <summary>Sinh viên chọn/đổi phương thức thanh toán trước khi trả tiền.</summary>
public class SelectPaymentMethodDto
{
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
}

/// <summary>Staff xác nhận đã thu tiền mặt hoặc đã nhận chuyển khoản.</summary>
public class ConfirmPaymentDto
{
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    /// <summary>Bắt buộc với VietQR: mã giao dịch ngân hàng để đối soát.</summary>
    public string? TransactionCode { get; set; }
}
