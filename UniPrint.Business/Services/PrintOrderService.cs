using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Business.Services;

public interface IPrintOrderService
{
    Task<ApiResponse<PrintOrderResponseDto>> CreateOrderAsync(Guid studentId, CreatePrintOrderDto request, CancellationToken ct = default);
    Task<ApiResponse<List<PrintOrderResponseDto>>> GetStudentOrdersAsync(Guid studentId, CancellationToken ct = default);
    Task<ApiResponse<List<PrintOrderResponseDto>>> GetStaffQueueOrdersAsync(CancellationToken ct = default);
    Task<ApiResponse<PrintOrderResponseDto>> GetOrderByIdAsync(Guid orderId, CancellationToken ct = default);
    Task<ApiResponse<bool>> UpdateOrderStatusAsync(Guid orderId, Guid staffId, PrintOrderStatus newStatus, CancellationToken ct = default);
    Task<ApiResponse<bool>> AssignShelfAsync(Guid orderId, Guid staffId, Guid shelfId, CancellationToken ct = default);
    Task<ApiResponse<bool>> ScanPickupQrAsync(string qrToken, Guid staffId, CancellationToken ct = default);
    Task<ApiResponse<bool>> CancelOrderAsync(Guid orderId, Guid studentId, CancellationToken ct = default);
}

public class PrintOrderService : IPrintOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPriceCalculatorService _priceCalculator;

    public PrintOrderService(IUnitOfWork unitOfWork, IPriceCalculatorService priceCalculator)
    {
        _unitOfWork = unitOfWork;
        _priceCalculator = priceCalculator;
    }

    public async Task<ApiResponse<PrintOrderResponseDto>> CreateOrderAsync(Guid studentId, CreatePrintOrderDto request, CancellationToken ct = default)
    {
        var doc = await _unitOfWork.Repository<Document>().GetByIdAsync(request.DocumentId, ct);
        if (doc == null)
        {
            return ApiResponse<PrintOrderResponseDto>.Fail("Không tìm thấy tài liệu cần in.");
        }

        // Tính giá và khóa giá cố định cho đơn hàng này (BR02)
        var priceCalc = await _priceCalculator.CalculatePriceAsync(new CalculatePriceRequestDto
        {
            PageCount = doc.PageCount,
            NumberOfCopies = request.NumberOfCopies,
            IsColor = request.IsColor,
            IsDoubleSided = request.IsDoubleSided,
            HasBinding = request.HasBinding,
            HasStaple = request.HasStaple
        }, ct);

        var printOption = new PrintOption
        {
            NumberOfCopies = request.NumberOfCopies,
            IsColor = request.IsColor,
            IsDoubleSided = request.IsDoubleSided,
            HasBinding = request.HasBinding,
            HasStaple = request.HasStaple,
            UnitPricePerPage = priceCalc.Data!.UnitPricePerPage,
            TotalPrice = priceCalc.Data!.TotalEstimatedPrice
        };
        await _unitOfWork.Repository<PrintOption>().AddAsync(printOption, ct);

        var orderCode = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var order = new PrintOrder
        {
            OrderCode = orderCode,
            StudentId = studentId,
            DocumentId = request.DocumentId,
            PrintOption = printOption,
            Status = PrintOrderStatus.Pending,
            Notes = request.Notes
        };
        await _unitOfWork.Repository<PrintOrder>().AddAsync(order, ct);

        // Tạo PickupQR
        var qrToken = $"QR-{Guid.NewGuid():N}";
        var pickupQr = new PickupQR
        {
            Order = order,
            QrToken = qrToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsUsed = false
        };
        await _unitOfWork.Repository<PickupQR>().AddAsync(pickupQr, ct);

        // Tạo Payment record
        var payment = new Payment
        {
            Order = order,
            Amount = printOption.TotalPrice,
            Method = request.PaymentMethod,
            Status = PaymentStatus.Pending
        };
        await _unitOfWork.Repository<Payment>().AddAsync(payment, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<PrintOrderResponseDto>.Ok(new PrintOrderResponseDto
        {
            Id = order.Id,
            OrderCode = order.OrderCode,
            StudentName = "Student",
            DocumentName = doc.FileName,
            NumberOfCopies = printOption.NumberOfCopies,
            IsColor = printOption.IsColor,
            IsDoubleSided = printOption.IsDoubleSided,
            TotalPrice = printOption.TotalPrice,
            Status = order.Status,
            QrToken = qrToken,
            PaymentStatus = payment.Status,
            CreatedAt = order.CreatedAt
        }, "Tạo đơn in thành công!");
    }

    public async Task<ApiResponse<List<PrintOrderResponseDto>>> GetStudentOrdersAsync(Guid studentId, CancellationToken ct = default)
    {
        var orders = await _unitOfWork.Repository<PrintOrder>().Query()
            .Include(o => o.Student)
            .Include(o => o.Document)
            .Include(o => o.PrintOption)
            .Include(o => o.Shelf)
            .Include(o => o.PickupQR)
            .Include(o => o.Payment)
            .Where(o => o.StudentId == studentId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new PrintOrderResponseDto
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                StudentName = o.Student.FullName,
                DocumentName = o.Document.FileName,
                NumberOfCopies = o.PrintOption.NumberOfCopies,
                IsColor = o.PrintOption.IsColor,
                IsDoubleSided = o.PrintOption.IsDoubleSided,
                TotalPrice = o.PrintOption.TotalPrice,
                Status = o.Status,
                ShelfCode = o.Shelf != null ? o.Shelf.ShelfCode : null,
                QrToken = o.PickupQR != null ? o.PickupQR.QrToken : null,
                PaymentStatus = o.Payment != null ? o.Payment.Status : PaymentStatus.Pending,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<PrintOrderResponseDto>>.Ok(orders);
    }

    public async Task<ApiResponse<List<PrintOrderResponseDto>>> GetStaffQueueOrdersAsync(CancellationToken ct = default)
    {
        var orders = await _unitOfWork.Repository<PrintOrder>().Query()
            .Include(o => o.Student)
            .Include(o => o.Document)
            .Include(o => o.PrintOption)
            .Include(o => o.Shelf)
            .Include(o => o.Payment)
            .Where(o => o.Status != PrintOrderStatus.Completed && o.Status != PrintOrderStatus.Cancelled)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new PrintOrderResponseDto
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                StudentName = o.Student.FullName,
                DocumentName = o.Document.FileName,
                NumberOfCopies = o.PrintOption.NumberOfCopies,
                IsColor = o.PrintOption.IsColor,
                IsDoubleSided = o.PrintOption.IsDoubleSided,
                TotalPrice = o.PrintOption.TotalPrice,
                Status = o.Status,
                ShelfCode = o.Shelf != null ? o.Shelf.ShelfCode : null,
                PaymentStatus = o.Payment != null ? o.Payment.Status : PaymentStatus.Pending,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<PrintOrderResponseDto>>.Ok(orders);
    }

    public async Task<ApiResponse<PrintOrderResponseDto>> GetOrderByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var o = await _unitOfWork.Repository<PrintOrder>().Query()
            .Include(o => o.Student)
            .Include(o => o.Document)
            .Include(o => o.PrintOption)
            .Include(o => o.Shelf)
            .Include(o => o.PickupQR)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (o == null) return ApiResponse<PrintOrderResponseDto>.Fail("Không tìm thấy đơn in.");

        return ApiResponse<PrintOrderResponseDto>.Ok(new PrintOrderResponseDto
        {
            Id = o.Id,
            OrderCode = o.OrderCode,
            StudentName = o.Student.FullName,
            DocumentName = o.Document.FileName,
            NumberOfCopies = o.PrintOption.NumberOfCopies,
            IsColor = o.PrintOption.IsColor,
            IsDoubleSided = o.PrintOption.IsDoubleSided,
            TotalPrice = o.PrintOption.TotalPrice,
            Status = o.Status,
            ShelfCode = o.Shelf?.ShelfCode,
            QrToken = o.PickupQR?.QrToken,
            PaymentStatus = o.Payment?.Status ?? PaymentStatus.Pending,
            CreatedAt = o.CreatedAt
        });
    }

    public async Task<ApiResponse<bool>> UpdateOrderStatusAsync(Guid orderId, Guid staffId, PrintOrderStatus newStatus, CancellationToken ct = default)
    {
        var order = await _unitOfWork.Repository<PrintOrder>().GetByIdAsync(orderId, ct);
        if (order == null) return ApiResponse<bool>.Fail("Không tìm thấy đơn hàng.");

        order.StaffId = staffId;
        order.Status = newStatus;
        _unitOfWork.Repository<PrintOrder>().Update(order);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, $"Đã cập nhật trạng thái đơn sang {newStatus}.");
    }

    public async Task<ApiResponse<bool>> AssignShelfAsync(Guid orderId, Guid staffId, Guid shelfId, CancellationToken ct = default)
    {
        var order = await _unitOfWork.Repository<PrintOrder>().GetByIdAsync(orderId, ct);
        if (order == null) return ApiResponse<bool>.Fail("Không tìm thấy đơn hàng.");

        var shelf = await _unitOfWork.Repository<Shelf>().GetByIdAsync(shelfId, ct);
        if (shelf == null || !shelf.IsAvailable)
        {
            return ApiResponse<bool>.Fail("Kệ lưu trữ không tồn tại hoặc đã đầy chỗ (EC06).");
        }

        order.StaffId = staffId;
        order.ShelfId = shelfId;
        order.Status = PrintOrderStatus.ReadyForPickup;

        shelf.CurrentCount += 1;
        _unitOfWork.Repository<Shelf>().Update(shelf);
        _unitOfWork.Repository<PrintOrder>().Update(order);

        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, $"Đã gán đơn hàng vào kệ {shelf.ShelfCode} và sẵn sàng nhận tài liệu.");
    }

    public async Task<ApiResponse<bool>> ScanPickupQrAsync(string qrToken, Guid staffId, CancellationToken ct = default)
    {
        var qr = await _unitOfWork.Repository<PickupQR>().Query()
            .Include(q => q.Order)
            .ThenInclude(o => o.Shelf)
            .FirstOrDefaultAsync(q => q.QrToken == qrToken, ct);

        if (qr == null)
        {
            return ApiResponse<bool>.Fail("Mã QR không hợp lệ.");
        }

        if (qr.IsUsed)
        {
            return ApiResponse<bool>.Fail("Mã QR này đã được sử dụng trước đó.");
        }

        if (qr.ExpiresAt < DateTime.UtcNow)
        {
            return ApiResponse<bool>.Fail("Mã QR đã hết hạn (EC04).");
        }

        // Đánh dấu hoàn tất
        qr.IsUsed = true;
        qr.UsedAt = DateTime.UtcNow;

        var order = qr.Order;
        order.StaffId = staffId;
        order.Status = PrintOrderStatus.Completed;

        // Giải phóng slot trên kệ
        if (order.Shelf != null && order.Shelf.CurrentCount > 0)
        {
            order.Shelf.CurrentCount -= 1;
            _unitOfWork.Repository<Shelf>().Update(order.Shelf);
        }

        _unitOfWork.Repository<PickupQR>().Update(qr);
        _unitOfWork.Repository<PrintOrder>().Update(order);

        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, "Xác nhận nhận tài liệu thành công. Đơn hàng đã hoàn tất!");
    }

    public async Task<ApiResponse<bool>> CancelOrderAsync(Guid orderId, Guid studentId, CancellationToken ct = default)
    {
        var order = await _unitOfWork.Repository<PrintOrder>().GetByIdAsync(orderId, ct);
        if (order == null || order.StudentId != studentId)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn hàng.");
        }

        // BR02: Khi Staff đã bắt đầu in (Printing trở đi) -> không được hủy
        if (order.Status >= PrintOrderStatus.Printing && order.Status != PrintOrderStatus.Cancelled)
        {
            return ApiResponse<bool>.Fail("Đơn hàng đã bắt đầu in hoặc đã hoàn tất, không thể hủy (BR02).");
        }

        order.Status = PrintOrderStatus.Cancelled;
        _unitOfWork.Repository<PrintOrder>().Update(order);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Hủy đơn hàng thành công.");
    }
}
