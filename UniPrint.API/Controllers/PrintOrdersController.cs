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
    private readonly IOrderTrackingService _trackingService;

    public PrintOrdersController(
        IPrintOrderService orderService,
        IPriceCalculatorService priceCalculator,
        IOrderTrackingService trackingService)
    {
        _orderService = orderService;
        _priceCalculator = priceCalculator;
        _trackingService = trackingService;
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
        // Pickup tokens must not be disclosed to another student.
        if (!User.IsInRole("Staff") && !User.IsInRole("Admin"))
        {
            var ownOrders = await _orderService.GetStudentOrdersAsync(GetCurrentUserId());
            if (ownOrders.Data == null || !ownOrders.Data.Any(order => order.Id == id))
                return NotFound();
        }
        var result = await _orderService.GetOrderByIdAsync(id);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Task 19: Chi tiết theo dõi đơn — tiến trình, vị trí kệ nhận hàng, mã QR nhận và hóa đơn thanh toán.
    /// Sinh viên chỉ xem được đơn của mình; Staff/Admin xem mọi đơn.
    /// </summary>
    [HttpGet("{id:guid}/tracking")]
    public async Task<IActionResult> GetOrderTracking(Guid id, CancellationToken ct)
    {
        var isStaffOrAdmin = User.IsInRole("Staff") || User.IsInRole("Admin");
        var result = await _trackingService.GetOrderTrackingAsync(id, GetCurrentUserId(), isStaffOrAdmin, ct);
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
