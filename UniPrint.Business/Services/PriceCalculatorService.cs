using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Business.Services;

public interface IPriceCalculatorService
{
    Task<ApiResponse<PriceCalculationResultDto>> CalculatePriceAsync(CalculatePriceRequestDto request, CancellationToken ct = default);
}

public class PriceCalculatorService : IPriceCalculatorService
{
    private readonly IUnitOfWork _unitOfWork;

    public PriceCalculatorService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PriceCalculationResultDto>> CalculatePriceAsync(CalculatePriceRequestDto request, CancellationToken ct = default)
    {
        var configRepo = _unitOfWork.Repository<PriceConfig>();
        var activeConfigs = await configRepo.FindAsync(c => c.IsActive, ct);
        var config = activeConfigs.FirstOrDefault() ?? new PriceConfig();

        decimal basePagePrice = request.IsColor ? config.PricePerColorPage : config.PricePerBWPage;

        // Nếu in 2 mặt, giảm giá phần trăm theo bảng giá
        if (request.IsDoubleSided && config.DoubleSidedDiscountPercent > 0)
        {
            basePagePrice -= (basePagePrice * (config.DoubleSidedDiscountPercent / 100m));
        }

        decimal pageSubtotal = basePagePrice * request.PageCount * request.NumberOfCopies;

        decimal extras = 0;
        if (request.HasBinding) extras += config.BindingPrice * request.NumberOfCopies;
        if (request.HasStaple) extras += config.StaplePrice * request.NumberOfCopies;

        decimal grandTotal = pageSubtotal + extras;

        return ApiResponse<PriceCalculationResultDto>.Ok(new PriceCalculationResultDto
        {
            UnitPricePerPage = basePagePrice,
            PageSubtotal = pageSubtotal,
            ExtraServicesTotal = extras,
            TotalEstimatedPrice = grandTotal
        });
    }
}
