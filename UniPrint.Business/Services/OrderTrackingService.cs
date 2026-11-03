using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Business.Services;

/// <summary>
/// Task 19 (Get Order Tracking Details): Xem chi tiết đơn hàng, mã đơn, vị trí kệ nhận hàng,
/// mã QR nhận tài liệu và hóa đơn thanh toán.
/// </summary>
public interface IOrderTrackingService
{
    Task<ApiResponse<OrderTrackingDto>> GetOrderTrackingAsync(Guid orderId, Guid userId, bool isStaffOrAdmin, CancellationToken ct = default);
}

public class OrderTrackingService : IOrderTrackingService
{
    private static readonly (PrintOrderStatus Status, string Label)[] Steps =
    {
        (PrintOrderStatus.Pending, "Đã đặt đơn"),
        (PrintOrderStatus.Processing, "Đang chuẩn bị"),
        (PrintOrderStatus.Printing, "Đang in"),
        (PrintOrderStatus.ReadyForPickup, "Sẵn sàng nhận tại kệ"),
        (PrintOrderStatus.Completed, "Đã nhận tài liệu")
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly VietQrSettings _vietQrSettings;

    public OrderTrackingService(IUnitOfWork unitOfWork, VietQrSettings vietQrSettings)
    {
        _unitOfWork = unitOfWork;
        _vietQrSettings = vietQrSettings;
    }

    public async Task<ApiResponse<OrderTrackingDto>> GetOrderTrackingAsync(Guid orderId, Guid userId, bool isStaffOrAdmin, CancellationToken ct = default)
    {
        var order = await _unitOfWork.Repository<PrintOrder>().Query()
            .AsNoTracking()
            .Include(o => o.Student)
            .Include(o => o.Document)
            .Include(o => o.PrintOption)
            .Include(o => o.Shelf)
            .Include(o => o.PickupQR)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        // Sinh viên chỉ xem được đơn của mình; không tiết lộ đơn tồn tại hay không.
        if (order == null || (!isStaffOrAdmin && order.StudentId != userId))
            return ApiResponse<OrderTrackingDto>.Fail("Không tìm thấy đơn in.");

        var now = DateTime.UtcNow;
        var dto = new OrderTrackingDto
        {
            Id = order.Id,
            OrderCode = order.OrderCode,
            Status = order.Status,
            StatusLabel = GetStatusLabel(order.Status),
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            Notes = order.Notes,
            CanCancel = order.Status is PrintOrderStatus.Pending or PrintOrderStatus.Processing
                        && order.Payment?.Status != PaymentStatus.Paid,
            StudentName = order.Student?.FullName ?? string.Empty,
            DocumentName = order.Document?.FileName ?? string.Empty,
            PageCount = order.Document?.PageCount ?? 0,
            NumberOfCopies = order.PrintOption.NumberOfCopies,
            IsColor = order.PrintOption.IsColor,
            IsDoubleSided = order.PrintOption.IsDoubleSided,
            HasBinding = order.PrintOption.HasBinding,
            HasStaple = order.PrintOption.HasStaple,
            UnitPricePerPage = order.PrintOption.UnitPricePerPage,
            TotalPrice = order.PrintOption.TotalPrice,
            ShelfCode = order.Shelf?.ShelfCode,
            ShelfLocation = order.Shelf?.Location,
            PickupQr = BuildPickupQr(order, now),
            Payment = order.Payment != null
                ? PaymentMapper.ToDto(order.Payment, order, _vietQrSettings)
                : BuildPendingCashInvoice(order),
            Timeline = BuildTimeline(order.Status)
        };

        return ApiResponse<OrderTrackingDto>.Ok(dto);
    }

    public static string GetStatusLabel(PrintOrderStatus status) => status switch
    {
        PrintOrderStatus.Pending => "Chờ xử lý",
        PrintOrderStatus.Processing => "Đang chuẩn bị",
        PrintOrderStatus.Printing => "Đang in",
        PrintOrderStatus.ShelfAssigned => "Đã vào kệ",
        PrintOrderStatus.ReadyForPickup => "Sẵn sàng nhận",
        PrintOrderStatus.Completed => "Hoàn thành",
        PrintOrderStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    private static PickupQrInfoDto? BuildPickupQr(PrintOrder order, DateTime now)
    {
        var qr = order.PickupQR;
        if (qr == null) return null;

        var expiresAt = DateTime.SpecifyKind(qr.ExpiresAt, DateTimeKind.Utc);
        var isExpired = expiresAt <= now;
        var waitingPickup = order.Status is PrintOrderStatus.ReadyForPickup or PrintOrderStatus.ShelfAssigned;
        var canPickup = waitingPickup && !qr.IsUsed && !isExpired;

        return new PickupQrInfoDto
        {
            QrToken = canPickup ? qr.QrToken : null,
            ExpiresAt = new DateTimeOffset(expiresAt),
            IsUsed = qr.IsUsed,
            UsedAt = qr.UsedAt.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(qr.UsedAt.Value, DateTimeKind.Utc)) : null,
            IsExpired = isExpired,
            CanPickup = canPickup
        };
    }

    /// <summary>Đơn cũ chưa có bản ghi Payment: hiển thị hóa đơn tiền mặt chờ thanh toán theo giá đã chốt.</summary>
    private static PaymentResponseDto BuildPendingCashInvoice(PrintOrder order) => new()
    {
        OrderId = order.Id,
        OrderCode = order.OrderCode,
        StudentName = order.Student?.FullName ?? string.Empty,
        OrderStatus = order.Status,
        Amount = order.PrintOption.TotalPrice,
        Method = PaymentMethod.Cash,
        Status = PaymentStatus.Pending,
        CreatedAt = order.CreatedAt
    };

    private static List<OrderTimelineStepDto> BuildTimeline(PrintOrderStatus status)
    {
        if (status == PrintOrderStatus.Cancelled)
        {
            return new List<OrderTimelineStepDto>
            {
                new() { Status = PrintOrderStatus.Pending, Label = Steps[0].Label, IsDone = true },
                new() { Status = PrintOrderStatus.Cancelled, Label = "Đơn đã bị hủy", IsDone = true, IsCurrent = true }
            };
        }

        var currentIndex = status switch
        {
            PrintOrderStatus.Pending => 0,
            PrintOrderStatus.Processing => 1,
            PrintOrderStatus.Printing => 2,
            PrintOrderStatus.ShelfAssigned or PrintOrderStatus.ReadyForPickup => 3,
            PrintOrderStatus.Completed => 4,
            _ => 0
        };

        return Steps.Select((step, index) => new OrderTimelineStepDto
        {
            Status = step.Status,
            Label = step.Label,
            IsDone = index <= currentIndex,
            IsCurrent = index == currentIndex
        }).ToList();
    }
}
