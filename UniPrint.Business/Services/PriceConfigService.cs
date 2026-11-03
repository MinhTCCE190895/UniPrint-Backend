using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Business.Services;

/// <summary>
/// Task 20 (Update Price Configuration): Admin cập nhật bảng giá in ấn hệ thống.
/// Mỗi lần cập nhật tạo một phiên bản mới (bản cũ chuyển IsActive = false) để giữ lịch sử.
/// Đơn đã tạo không bị ảnh hưởng vì giá đã được chốt trong PrintOption (BR02).
/// </summary>
public interface IPriceConfigService
{
    Task<ApiResponse<PriceConfigDto>> GetActiveConfigAsync(CancellationToken ct = default);
    Task<ApiResponse<PriceConfigDto>> UpdatePriceConfigAsync(Guid adminId, UpdatePriceConfigDto request, CancellationToken ct = default);
    Task<ApiResponse<List<PriceConfigDto>>> GetHistoryAsync(int take = 10, CancellationToken ct = default);
}

public class PriceConfigService : IPriceConfigService
{
    public const decimal MaxPrice = 1_000_000m;

    private readonly IUnitOfWork _unitOfWork;

    public PriceConfigService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PriceConfigDto>> GetActiveConfigAsync(CancellationToken ct = default)
    {
        var active = await _unitOfWork.Repository<PriceConfig>().Query()
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        // Chưa có cấu hình trong DB -> trả bảng giá mặc định giống PriceCalculatorService.
        return ApiResponse<PriceConfigDto>.Ok(ToDto(active ?? new PriceConfig { Id = Guid.Empty }));
    }

    public async Task<ApiResponse<PriceConfigDto>> UpdatePriceConfigAsync(Guid adminId, UpdatePriceConfigDto request, CancellationToken ct = default)
    {
        var admin = await _unitOfWork.Repository<User>().GetByIdAsync(adminId, ct);
        if (admin == null || !admin.IsActive || admin.Role != UserRole.Admin)
            return ApiResponse<PriceConfigDto>.Fail("Chỉ quản trị viên được cập nhật bảng giá.");

        var errors = Validate(request);
        if (errors.Count > 0)
            return ApiResponse<PriceConfigDto>.Fail("Bảng giá không hợp lệ.", errors);

        var repo = _unitOfWork.Repository<PriceConfig>();
        var activeConfigs = (await repo.FindAsync(c => c.IsActive, ct)).ToList();
        var current = activeConfigs.OrderByDescending(c => c.CreatedAt).FirstOrDefault();

        if (current != null
            && current.PricePerBWPage == request.PricePerBWPage
            && current.PricePerColorPage == request.PricePerColorPage
            && current.DoubleSidedDiscountPercent == request.DoubleSidedDiscountPercent
            && current.BindingPrice == request.BindingPrice
            && current.StaplePrice == request.StaplePrice)
        {
            return ApiResponse<PriceConfigDto>.Fail("Bảng giá không có thay đổi so với hiện tại.");
        }

        foreach (var config in activeConfigs)
        {
            config.IsActive = false;
            repo.Update(config);
        }

        var newConfig = new PriceConfig
        {
            PricePerBWPage = request.PricePerBWPage,
            PricePerColorPage = request.PricePerColorPage,
            DoubleSidedDiscountPercent = request.DoubleSidedDiscountPercent,
            BindingPrice = request.BindingPrice,
            StaplePrice = request.StaplePrice,
            IsActive = true
        };
        await repo.AddAsync(newConfig, ct);

        // Một SaveChanges = một transaction: luôn chỉ có đúng 1 bảng giá đang áp dụng.
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<PriceConfigDto>.Ok(ToDto(newConfig),
            "Đã cập nhật bảng giá. Các đơn đã tạo trước đó giữ nguyên giá đã chốt (BR02).");
    }

    public async Task<ApiResponse<List<PriceConfigDto>>> GetHistoryAsync(int take = 10, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 50);
        var configs = await _unitOfWork.Repository<PriceConfig>().Query()
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
        return ApiResponse<List<PriceConfigDto>>.Ok(configs.Select(ToDto).ToList());
    }

    public static List<string> Validate(UpdatePriceConfigDto? request)
    {
        var errors = new List<string>();
        if (request == null)
        {
            errors.Add("Thiếu dữ liệu bảng giá.");
            return errors;
        }

        if (request.PricePerBWPage <= 0)
            errors.Add("Giá in đen trắng mỗi trang phải lớn hơn 0.");
        if (request.PricePerColorPage <= 0)
            errors.Add("Giá in màu mỗi trang phải lớn hơn 0.");
        if (request.PricePerColorPage > 0 && request.PricePerBWPage > 0 && request.PricePerColorPage < request.PricePerBWPage)
            errors.Add("Giá in màu không được thấp hơn giá in đen trắng.");
        if (request.DoubleSidedDiscountPercent < 0 || request.DoubleSidedDiscountPercent >= 100)
            errors.Add("Chiết khấu in 2 mặt phải từ 0% đến dưới 100%.");
        if (request.BindingPrice < 0)
            errors.Add("Phí đóng gáy không được âm.");
        if (request.StaplePrice < 0)
            errors.Add("Phí bấm ghim không được âm.");

        if (new[] { request.PricePerBWPage, request.PricePerColorPage, request.BindingPrice, request.StaplePrice }.Any(p => p > MaxPrice))
            errors.Add($"Mỗi mức giá tối đa {MaxPrice:N0}đ.");

        return errors;
    }

    private static PriceConfigDto ToDto(PriceConfig c) => new()
    {
        Id = c.Id,
        PricePerBWPage = c.PricePerBWPage,
        PricePerColorPage = c.PricePerColorPage,
        DoubleSidedDiscountPercent = c.DoubleSidedDiscountPercent,
        BindingPrice = c.BindingPrice,
        StaplePrice = c.StaplePrice,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
