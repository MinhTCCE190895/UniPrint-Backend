using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PrintOrdersController : ControllerBase
{
    private readonly IPrintOrderService _orderService;
    private readonly IPriceCalculatorService _priceCalculator;

    public PrintOrdersController(IPrintOrderService orderService, IPriceCalculatorService priceCalculator)
    {
        _orderService = orderService;
        _priceCalculator = priceCalculator;
    }

    [AllowAnonymous]
    [HttpPost("calculate-price")]
    public async Task<IActionResult> CalculatePrice([FromBody] CalculatePriceRequestDto request)
    {
        var result = await _priceCalculator.CalculatePriceAsync(request);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CreateOrder([FromBody] CreatePrintOrderDto request)
    {
        var studentId = GetCurrentUserId();
        var result = await _orderService.CreateOrderAsync(studentId, request);
        if (!result.Success) return BadRequest(result);
        return CreatedAtAction(nameof(GetOrderById), new { id = result.Data!.Id }, result);
    }

    [HttpGet("my-orders")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyOrders()
    {
        var studentId = GetCurrentUserId();
        var result = await _orderService.GetStudentOrdersAsync(studentId);
        return Ok(result);
    }

    [HttpGet("queue")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> GetStaffQueue()
    {
        var result = await _orderService.GetStaffQueueOrdersAsync();
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderById(Guid id)
    {
        var result = await _orderService.GetOrderByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusDto dto)
    {
        var staffId = GetCurrentUserId();
        var result = await _orderService.UpdateOrderStatusAsync(id, staffId, dto.NewStatus);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{id}/assign-shelf")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> AssignShelf(Guid id, [FromBody] AssignShelfDto dto)
    {
        var staffId = GetCurrentUserId();
        var result = await _orderService.AssignShelfAsync(id, staffId, dto.ShelfId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("scan-pickup-qr")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> ScanPickupQr([FromQuery] string qrToken)
    {
        var staffId = GetCurrentUserId();
        var result = await _orderService.ScanPickupQrAsync(qrToken, staffId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("{id}/cancel")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CancelOrder(Guid id)
    {
        var studentId = GetCurrentUserId();
        var result = await _orderService.CancelOrderAsync(id, studentId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdStr!);
    }
}
