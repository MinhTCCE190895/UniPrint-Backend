using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Enums;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.Pickup;

/// <summary>
/// Task 18: Staff thu tiền mặt khi giao tài liệu hoặc xác nhận đã nhận chuyển khoản VietQR qua REST API.
/// </summary>
[Authorize(Roles = "Staff,Admin")]
public class PaymentsModel : PageModel
{
    private readonly WebApiClient _api;

    public PaymentsModel(WebApiClient api)
    {
        _api = api;
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
        var url = "api/payments/unpaid";
        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            url += $"?keyword={Uri.EscapeDataString(Keyword.Trim())}";
        }

        var result = await _api.GetAsync<ApiResponse<List<PaymentResponseDto>>>(url);
        UnpaidPayments = result?.Data ?? new();
    }

    public async Task<IActionResult> OnPostConfirmAsync(Guid orderId, PaymentMethod method, string? transactionCode)
    {
        var result = await _api.PostAsync<ApiResponse<PaymentResponseDto>>($"api/payments/order/{orderId}/confirm", new ConfirmPaymentDto
        {
            Method = method,
            TransactionCode = transactionCode
        });

        IsSuccess = result != null && result.Success;
        Message = result != null && result.Success
            ? $"{result.Data?.OrderCode}: {result.Message}"
            : (result?.Message ?? "Xác nhận thanh toán thất bại.");

        return RedirectToPage(new { Keyword });
    }
}
