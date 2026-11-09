using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Enums;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.Orders;

/// <summary>
/// Task 19: Trang theo dõi đơn in — tiến trình, vị trí kệ, mã QR nhận tài liệu và hóa đơn thanh toán qua REST API.
/// Sinh viên chỉ xem đơn của mình; Staff/Admin xem mọi đơn.
/// </summary>
[Authorize]
public class TrackingModel : PageModel
{
    private readonly WebApiClient _api;

    public TrackingModel(WebApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public OrderTrackingDto? Tracking { get; set; }
    public bool IsStudentOwner { get; set; }

    [TempData]
    public string? Message { get; set; }

    [TempData]
    public bool IsSuccess { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var result = await _api.GetAsync<ApiResponse<OrderTrackingDto>>($"api/printorders/{Id}/tracking");
        if (result == null || !result.Success || result.Data == null)
            return NotFound();

        Tracking = result.Data;
        IsStudentOwner = User.IsInRole("Student");
        return Page();
    }

    /// <summary>Task 18: Sinh viên đổi phương thức thanh toán ngay trên trang theo dõi.</summary>
    public async Task<IActionResult> OnPostSelectMethodAsync(PaymentMethod method)
    {
        if (!User.IsInRole("Student")) return Forbid();

        var result = await _api.PutAsync<ApiResponse<PaymentResponseDto>>($"api/payments/order/{Id}/method", new SelectPaymentMethodDto
        {
            Method = method
        });

        IsSuccess = result != null && result.Success;
        Message = result?.Message ?? "Cập nhật phương thức thanh toán thất bại.";
        return RedirectToPage(new { id = Id });
    }
}
