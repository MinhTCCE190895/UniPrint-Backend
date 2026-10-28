using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Enums;

namespace UniPrint.Web.Pages.Orders;

/// <summary>
/// Task 19: Trang theo dõi đơn in — tiến trình, vị trí kệ, mã QR nhận tài liệu và hóa đơn thanh toán.
/// Sinh viên chỉ xem đơn của mình; Staff/Admin xem mọi đơn.
/// </summary>
[Authorize]
public class TrackingModel : PageModel
{
    private readonly IOrderTrackingService _trackingService;
    private readonly IPaymentService _paymentService;

    public TrackingModel(IOrderTrackingService trackingService, IPaymentService paymentService)
    {
        _trackingService = trackingService;
        _paymentService = paymentService;
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
        if (!TryGetUserId(out var userId)) return Challenge();

        var result = await _trackingService.GetOrderTrackingAsync(Id, userId, IsStaffOrAdmin());
        if (!result.Success) return NotFound();

        Tracking = result.Data;
        IsStudentOwner = User.IsInRole("Student");
        return Page();
    }

    /// <summary>Task 18: Sinh viên đổi phương thức thanh toán ngay trên trang theo dõi.</summary>
    public async Task<IActionResult> OnPostSelectMethodAsync(PaymentMethod method)
    {
        if (!TryGetUserId(out var userId)) return Challenge();
        if (!User.IsInRole("Student")) return Forbid();

        var result = await _paymentService.SelectPaymentMethodAsync(Id, userId, method);
        IsSuccess = result.Success;
        Message = result.Message;
        return RedirectToPage(new { id = Id });
    }

    private bool IsStaffOrAdmin() => User.IsInRole("Staff") || User.IsInRole("Admin");

    private bool TryGetUserId(out Guid userId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
