using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Enums;

namespace UniPrint.Web.Pages.Pickup;

/// <summary>
/// Task 18: Staff thu tiền mặt khi giao tài liệu hoặc xác nhận đã nhận chuyển khoản VietQR.
/// </summary>
[Authorize(Roles = "Staff,Admin")]
public class PaymentsModel : PageModel
{
    private readonly IPaymentService _paymentService;

    public PaymentsModel(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    public List<PaymentResponseDto> UnpaidPayments { get; set; } = new();

    [TempData]
    public string? Message { get; set; }

    [TempData]
    public bool IsSuccess { get; set; }

    public async Task OnGetAsync()
    {
        var result = await _paymentService.GetUnpaidPaymentsAsync(Keyword);
        UnpaidPayments = result.Data ?? new();
    }

    public async Task<IActionResult> OnPostConfirmAsync(Guid orderId, PaymentMethod method, string? transactionCode)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId))
            return Challenge();

        var result = await _paymentService.ConfirmPaymentAsync(orderId, staffId, new ConfirmPaymentDto
        {
            Method = method,
            TransactionCode = transactionCode
        });

        IsSuccess = result.Success;
        Message = result.Success
            ? $"{result.Data!.OrderCode}: {result.Message}"
            : result.Message;
        return RedirectToPage(new { Keyword });
    }
}
