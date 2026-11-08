using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Business.Services;

public interface IShelfService
{
    Task<ApiResponse<List<ShelfResponseDto>>> GetAllShelvesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<ShelfResponseDto>>> GetAvailableShelvesAsync(CancellationToken ct = default);
    Task<ApiResponse<ShelfDetailDto>> GetShelfByIdAsync(Guid shelfId, CancellationToken ct = default);
    Task<ApiResponse<ShelfInventoryResponseDto>> GetShelfInventoryAsync(CancellationToken ct = default);
    Task<ApiResponse<ShelfResponseDto>> CreateShelfAsync(CreateShelfDto dto, CancellationToken ct = default);
    Task<ApiResponse<ShelfResponseDto>> UpdateShelfAsync(Guid shelfId, UpdateShelfDto dto, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteShelfAsync(Guid shelfId, CancellationToken ct = default);
}

public class ShelfService : IShelfService
{
    private readonly IUnitOfWork _unitOfWork;

    public ShelfService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<List<ShelfResponseDto>>> GetAllShelvesAsync(CancellationToken ct = default)
    {
        var shelves = await _unitOfWork.Repository<Shelf>().Query()
            .AsNoTracking()
            .OrderBy(s => s.ShelfCode)
            .Select(s => new ShelfResponseDto
            {
                Id = s.Id,
                ShelfCode = s.ShelfCode,
                Location = s.Location,
                MaxCapacity = s.MaxCapacity,
                CurrentCount = s.CurrentCount,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<ShelfResponseDto>>.Ok(shelves);
    }

    public async Task<ApiResponse<List<ShelfResponseDto>>> GetAvailableShelvesAsync(CancellationToken ct = default)
    {
        var shelves = await _unitOfWork.Repository<Shelf>().Query()
            .AsNoTracking()
            .Where(s => s.CurrentCount < s.MaxCapacity)
            .OrderBy(s => s.ShelfCode)
            .Select(s => new ShelfResponseDto
            {
                Id = s.Id,
                ShelfCode = s.ShelfCode,
                Location = s.Location,
                MaxCapacity = s.MaxCapacity,
                CurrentCount = s.CurrentCount,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<ShelfResponseDto>>.Ok(shelves);
    }

    public async Task<ApiResponse<ShelfDetailDto>> GetShelfByIdAsync(Guid shelfId, CancellationToken ct = default)
    {
        var shelf = await _unitOfWork.Repository<Shelf>().GetByIdAsync(shelfId, ct);
        if (shelf == null)
        {
            return ApiResponse<ShelfDetailDto>.Fail("Không tìm thấy kệ lưu trữ.");
        }

        var orders = await _unitOfWork.Repository<PrintOrder>().Query()
            .AsNoTracking()
            .Include(o => o.Student)
            .Include(o => o.Document)
            .Include(o => o.PrintOption)
            .Include(o => o.PickupQR)
            .Include(o => o.Payment)
            .Where(o => o.ShelfId == shelfId && o.Status != PrintOrderStatus.Completed && o.Status != PrintOrderStatus.Cancelled)
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
                ShelfCode = shelf.ShelfCode,
                QrToken = o.PickupQR != null ? o.PickupQR.QrToken : null,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(ct);

        var detail = new ShelfDetailDto
        {
            Id = shelf.Id,
            ShelfCode = shelf.ShelfCode,
            Location = shelf.Location,
            MaxCapacity = shelf.MaxCapacity,
            CurrentCount = shelf.CurrentCount,
            CreatedAt = shelf.CreatedAt,
            Orders = orders
        };

        return ApiResponse<ShelfDetailDto>.Ok(detail);
    }

    public async Task<ApiResponse<ShelfInventoryResponseDto>> GetShelfInventoryAsync(CancellationToken ct = default)
    {
        var shelves = await _unitOfWork.Repository<Shelf>().Query()
            .AsNoTracking()
            .OrderBy(s => s.ShelfCode)
            .ToListAsync(ct);

        var activeOrders = await _unitOfWork.Repository<PrintOrder>().Query()
            .AsNoTracking()
            .Include(o => o.Student)
            .Include(o => o.Document)
            .Include(o => o.PrintOption)
            .Include(o => o.Payment)
            .Where(o => o.ShelfId != null && o.Status != PrintOrderStatus.Completed && o.Status != PrintOrderStatus.Cancelled)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(ct);

        var ordersByShelf = activeOrders
            .GroupBy(o => o.ShelfId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(o => new ShelfInventoryOrderDto
            {
                OrderId = o.Id,
                OrderCode = o.OrderCode,
                StudentName = o.Student != null ? o.Student.FullName : "N/A",
                DocumentName = o.Document != null ? o.Document.FileName : "N/A",
                NumberOfCopies = o.PrintOption != null ? o.PrintOption.NumberOfCopies : 1,
                IsColor = o.PrintOption != null && o.PrintOption.IsColor,
                IsDoubleSided = o.PrintOption != null && o.PrintOption.IsDoubleSided,
                TotalPrice = o.PrintOption != null ? o.PrintOption.TotalPrice : 0,
                Status = o.Status.ToString(),
                IsPaid = o.Payment != null && o.Payment.Status == PaymentStatus.Paid,
                CreatedAt = o.CreatedAt
            }).ToList());

        var items = shelves.Select(s => new ShelfInventoryItemDto
        {
            Id = s.Id,
            ShelfCode = s.ShelfCode,
            Location = s.Location,
            MaxCapacity = s.MaxCapacity,
            CurrentCount = s.CurrentCount,
            CreatedAt = s.CreatedAt,
            StoredOrders = ordersByShelf.TryGetValue(s.Id, out var list) ? list : new List<ShelfInventoryOrderDto>()
        }).ToList();

        var totalShelves = shelves.Count;
        var totalCapacity = shelves.Sum(s => s.MaxCapacity);
        var totalStoredOrders = shelves.Sum(s => s.CurrentCount);
        var availableShelvesCount = shelves.Count(s => s.CurrentCount < s.MaxCapacity);
        var fullShelvesCount = shelves.Count(s => s.CurrentCount >= s.MaxCapacity);
        var overallOccupancyRate = totalCapacity > 0 ? Math.Round((double)totalStoredOrders / totalCapacity * 100, 1) : 0;

        var summary = new ShelfInventorySummaryDto
        {
            TotalShelves = totalShelves,
            TotalCapacity = totalCapacity,
            TotalStoredOrders = totalStoredOrders,
            AvailableShelvesCount = availableShelvesCount,
            FullShelvesCount = fullShelvesCount,
            OverallOccupancyRate = overallOccupancyRate
        };

        var response = new ShelfInventoryResponseDto
        {
            Summary = summary,
            Shelves = items
        };

        return ApiResponse<ShelfInventoryResponseDto>.Ok(response);
    }

    public async Task<ApiResponse<ShelfResponseDto>> CreateShelfAsync(CreateShelfDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.ShelfCode))
        {
            return ApiResponse<ShelfResponseDto>.Fail("Mã kệ không được để trống.");
        }

        var shelfCode = dto.ShelfCode.Trim().ToUpper();

        var exists = await _unitOfWork.Repository<Shelf>().ExistsAsync(s => s.ShelfCode.ToUpper() == shelfCode, ct);
        if (exists)
        {
            return ApiResponse<ShelfResponseDto>.Fail($"Mã kệ '{shelfCode}' đã tồn tại trong hệ thống.");
        }

        var maxCapacity = dto.MaxCapacity <= 0 ? 20 : dto.MaxCapacity;

        var shelf = new Shelf
        {
            ShelfCode = shelfCode,
            Location = string.IsNullOrWhiteSpace(dto.Location) ? "Main Library Floor 1" : dto.Location.Trim(),
            MaxCapacity = maxCapacity,
            CurrentCount = 0
        };

        await _unitOfWork.Repository<Shelf>().AddAsync(shelf, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var result = new ShelfResponseDto
        {
            Id = shelf.Id,
            ShelfCode = shelf.ShelfCode,
            Location = shelf.Location,
            MaxCapacity = shelf.MaxCapacity,
            CurrentCount = shelf.CurrentCount,
            CreatedAt = shelf.CreatedAt
        };

        return ApiResponse<ShelfResponseDto>.Ok(result, $"Tạo kệ '{shelf.ShelfCode}' thành công (Sức chứa tối đa: {shelf.MaxCapacity}).");
    }

    public async Task<ApiResponse<ShelfResponseDto>> UpdateShelfAsync(Guid shelfId, UpdateShelfDto dto, CancellationToken ct = default)
    {
        var shelf = await _unitOfWork.Repository<Shelf>().GetByIdAsync(shelfId, ct);
        if (shelf == null)
        {
            return ApiResponse<ShelfResponseDto>.Fail("Không tìm thấy kệ lưu trữ cần cập nhật.");
        }

        if (string.IsNullOrWhiteSpace(dto.ShelfCode))
        {
            return ApiResponse<ShelfResponseDto>.Fail("Mã kệ không được để trống.");
        }

        var shelfCode = dto.ShelfCode.Trim().ToUpper();

        // Kiểm tra nếu đổi mã kệ thì không được trùng với kệ khác
        if (!string.Equals(shelf.ShelfCode, shelfCode, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _unitOfWork.Repository<Shelf>().ExistsAsync(s => s.Id != shelfId && s.ShelfCode.ToUpper() == shelfCode, ct);
            if (exists)
            {
                return ApiResponse<ShelfResponseDto>.Fail($"Mã kệ '{shelfCode}' đã được sử dụng bởi kệ khác.");
            }
        }

        if (dto.MaxCapacity <= 0)
        {
            return ApiResponse<ShelfResponseDto>.Fail("Sức chứa tối đa của kệ phải lớn hơn 0.");
        }

        // Kiểm tra sức chứa: Không thể giảm sức chứa tối đa nhỏ hơn số lượng tài liệu đang có trên kệ
        if (dto.MaxCapacity < shelf.CurrentCount)
        {
            return ApiResponse<ShelfResponseDto>.Fail($"Không thể giảm sức chứa tối đa xuống {dto.MaxCapacity} vì kệ đang chứa {shelf.CurrentCount} tài liệu.");
        }

        shelf.ShelfCode = shelfCode;
        shelf.Location = string.IsNullOrWhiteSpace(dto.Location) ? shelf.Location : dto.Location.Trim();
        shelf.MaxCapacity = dto.MaxCapacity;

        _unitOfWork.Repository<Shelf>().Update(shelf);
        await _unitOfWork.SaveChangesAsync(ct);

        var result = new ShelfResponseDto
        {
            Id = shelf.Id,
            ShelfCode = shelf.ShelfCode,
            Location = shelf.Location,
            MaxCapacity = shelf.MaxCapacity,
            CurrentCount = shelf.CurrentCount,
            CreatedAt = shelf.CreatedAt
        };

        return ApiResponse<ShelfResponseDto>.Ok(result, $"Cập nhật kệ '{shelf.ShelfCode}' thành công.");
    }

    public async Task<ApiResponse<bool>> DeleteShelfAsync(Guid shelfId, CancellationToken ct = default)
    {
        var shelf = await _unitOfWork.Repository<Shelf>().GetByIdAsync(shelfId, ct);
        if (shelf == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy kệ lưu trữ cần xóa.");
        }

        // Không cho phép xóa kệ đang có tài liệu lưu trữ
        if (shelf.CurrentCount > 0)
        {
            return ApiResponse<bool>.Fail($"Không thể xóa kệ '{shelf.ShelfCode}' vì đang chứa {shelf.CurrentCount} đơn hàng lưu trữ.");
        }

        // Kiểm tra xem có đơn hàng nào đang trong tiến trình (chưa hoàn thành/hủy) gán kệ này không
        var hasActiveOrders = await _unitOfWork.Repository<PrintOrder>().ExistsAsync(
            o => o.ShelfId == shelfId && o.Status != PrintOrderStatus.Completed && o.Status != PrintOrderStatus.Cancelled, ct);
        if (hasActiveOrders)
        {
            return ApiResponse<bool>.Fail($"Không thể xóa kệ '{shelf.ShelfCode}' vì có đơn hàng đang hoạt động liên kết với kệ.");
        }

        _unitOfWork.Repository<Shelf>().Delete(shelf);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, $"Đã xóa kệ '{shelf.ShelfCode}' thành công.");
    }
}
