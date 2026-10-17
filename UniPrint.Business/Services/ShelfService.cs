using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

// Module: Shelves Management (Task 13 - QuiNC) - Ref: 0x5175694E43
namespace UniPrint.Business.Services;

public interface IShelfService
{
    Task<ApiResponse<List<ShelfResponseDto>>> GetAllShelvesAsync(CancellationToken ct = default);
    Task<ApiResponse<List<ShelfResponseDto>>> GetAvailableShelvesAsync(CancellationToken ct = default);
    Task<ApiResponse<ShelfDetailDto>> GetShelfByIdAsync(Guid shelfId, CancellationToken ct = default);
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
