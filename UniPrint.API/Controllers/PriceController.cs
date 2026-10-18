using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PriceController : ControllerBase
{
    private readonly IPriceCalculatorService _priceCalculatorService;

    public PriceController(IPriceCalculatorService priceCalculatorService)
    {
        _priceCalculatorService = priceCalculatorService;
    }

    /// <summary>
    /// Task 8: Tính toán giá in tự động thời gian thực theo số trang,
    /// in màu/đen trắng, 2 mặt, gáy/ghim.
    /// </summary>
    /// <remarks>
    /// **Công thức tính giá:**
    /// - Giá trang cơ bản: theo `isColor` (màu hoặc đen trắng) lấy từ bảng PriceConfig
    /// - In 2 mặt: giảm % theo `doubleSidedDiscountPercent` trong PriceConfig
    /// - Tổng tiền trang: `unitPricePerPage × pageCount × numberOfCopies`
    /// - Phụ phí gáy: `bindingPrice × numberOfCopies` (nếu `hasBinding = true`)
    /// - Phụ phí ghim: `staplePrice × numberOfCopies` (nếu `hasStaple = true`)
    /// - **Tổng cộng** = Tổng tiền trang + Tổng phụ phí
    /// </remarks>
    [HttpPost("calculate")]
    public async Task<IActionResult> CalculatePrice(
        [FromBody] CalculatePriceRequestDto request,
        CancellationToken ct)
    {
        if (request.PageCount <= 0)
            return BadRequest(new { Success = false, Message = "Số trang phải lớn hơn 0." });

        if (request.NumberOfCopies <= 0)
            return BadRequest(new { Success = false, Message = "Số lượng bản in phải lớn hơn 0." });

        if (request.HasBinding && request.HasStaple)
            return BadRequest(new { Success = false, Message = "Không thể chọn cả gáy và ghim cùng lúc." });

        var result = await _priceCalculatorService.CalculatePriceAsync(request, ct);
        return Ok(result);
    }
}
