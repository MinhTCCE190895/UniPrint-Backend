using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.API.Controllers;

/// <summary>
/// Task 18: Process Payment — thanh toán tiền mặt lúc nhận hàng (Cash) hoặc chuyển khoản VietQR.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>Xem hóa đơn của đơn in (sinh viên chủ đơn hoặc Staff/Admin). Có mã VietQR nếu chọn chuyển khoản.</summary>
    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrder(Guid orderId, CancellationToken ct)
    {
        var result = await _paymentService.GetPaymentByOrderAsync(orderId, GetCurrentUserId(), IsStaffOrAdmin(), ct);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>Sinh viên chọn phương thức: 1 = Tiền mặt khi nhận, 2 = Chuyển khoản VietQR.</summary>
    [HttpPut("order/{orderId:guid}/method")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SelectMethod(Guid orderId, [FromBody] SelectPaymentMethodDto dto, CancellationToken ct)
    {
        var result = await _paymentService.SelectPaymentMethodAsync(orderId, GetCurrentUserId(), dto.Method, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Staff xác nhận đã thu tiền mặt hoặc đã nhận chuyển khoản (kèm mã giao dịch).</summary>
    [HttpPost("order/{orderId:guid}/confirm")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> Confirm(Guid orderId, [FromBody] ConfirmPaymentDto dto, CancellationToken ct)
    {
        var result = await _paymentService.ConfirmPaymentAsync(orderId, GetCurrentUserId(), dto, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Danh sách hóa đơn chưa thanh toán (lọc theo mã đơn hoặc tên sinh viên).</summary>
    [HttpGet("unpaid")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> GetUnpaid([FromQuery] string? keyword, CancellationToken ct)
    {
        var result = await _paymentService.GetUnpaidPaymentsAsync(keyword, ct);
        return Ok(result);
    }

    private bool IsStaffOrAdmin() => User.IsInRole("Staff") || User.IsInRole("Admin");

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdStr!);
    }
}
