using System.Security.Claims;
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
    private readonly IPriceConfigService _priceConfigService;

    public PriceController(IPriceCalculatorService priceCalculatorService, IPriceConfigService priceConfigService)
    {
        _priceCalculatorService = priceCalculatorService;
        _priceConfigService = priceConfigService;
    }

    /// <summary>Task 20: Bảng giá đang áp dụng (công khai để app hiển thị đơn giá).</summary>
    [AllowAnonymous]
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
    {
        var result = await _priceConfigService.GetActiveConfigAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Task 20: Admin cập nhật bảng giá (giá trang màu, đen trắng, chiết khấu 2 mặt, gáy/ghim).
    /// Tạo phiên bản bảng giá mới; đơn đã tạo giữ nguyên giá đã chốt (BR02).
    /// </summary>
    [HttpPut("config")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateConfig([FromBody] UpdatePriceConfigDto request, CancellationToken ct)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _priceConfigService.UpdatePriceConfigAsync(adminId, request, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Task 20: Lịch sử các phiên bản bảng giá (mới nhất trước).</summary>
    [HttpGet("config/history")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetConfigHistory([FromQuery] int take = 10, CancellationToken ct = default)
    {
        var result = await _priceConfigService.GetHistoryAsync(take, ct);
        return Ok(result);
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
