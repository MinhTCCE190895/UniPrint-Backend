using UniPrint.DataAccess.Enums;

namespace UniPrint.Business.DTOs;

/// <summary>
/// Task 19: Chi tiết theo dõi đơn in — mã đơn, tiến trình, vị trí kệ nhận hàng, mã QR và hóa đơn thanh toán.
/// </summary>
public class OrderTrackingDto
{
    public Guid Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public PrintOrderStatus Status { get; set; }
    public string StatusText => Status.ToString();
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Notes { get; set; }
    public bool CanCancel { get; set; }

    public string StudentName { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public int PageCount { get; set; }

    public int NumberOfCopies { get; set; }
    public bool IsColor { get; set; }
    public bool IsDoubleSided { get; set; }
    public bool HasBinding { get; set; }
    public bool HasStaple { get; set; }
    public decimal UnitPricePerPage { get; set; }
    public decimal TotalPrice { get; set; }

    public string? ShelfCode { get; set; }
    public string? ShelfLocation { get; set; }

    public PickupQrInfoDto? PickupQr { get; set; }
    public PaymentResponseDto? Payment { get; set; }
    public List<OrderTimelineStepDto> Timeline { get; set; } = new();
}

public class PickupQrInfoDto
{
    /// <summary>Chỉ trả token khi đơn đang chờ nhận, chưa dùng và còn hạn.</summary>
    public string? QrToken { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public bool IsExpired { get; set; }
    public bool CanPickup { get; set; }
}

public class OrderTimelineStepDto
{
    public PrintOrderStatus Status { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public bool IsCurrent { get; set; }
}
