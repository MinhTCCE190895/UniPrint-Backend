using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Enums;

namespace UniPrint.Web.Pages.Student;

[Authorize(Roles = "Student")]
public class OrdersModel : PageModel
{
    private readonly IPrintOrderService _orderService;

    public OrdersModel(IPrintOrderService orderService)
    {
        _orderService = orderService;
    }

    public List<PrintOrderResponseDto> Orders { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public int TotalOrdersCount { get; set; }
    public int PendingCount { get; set; }
    public int InProgressCount { get; set; }
    public int ReadyForPickupCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var studentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(studentIdStr, out var studentId))
        {
            return RedirectToPage("/Auth/Login");
        }

        var result = await _orderService.GetStudentOrdersAsync(studentId);
        if (result.Success && result.Data != null)
        {
            var allOrders = result.Data;
            TotalOrdersCount = allOrders.Count;
            PendingCount = allOrders.Count(o => o.Status == PrintOrderStatus.Pending);
            InProgressCount = allOrders.Count(o => o.Status == PrintOrderStatus.Processing || o.Status == PrintOrderStatus.Printing || o.Status == PrintOrderStatus.ShelfAssigned);
            ReadyForPickupCount = allOrders.Count(o => o.Status == PrintOrderStatus.ReadyForPickup);
            CompletedCount = allOrders.Count(o => o.Status == PrintOrderStatus.Completed);
            CancelledCount = allOrders.Count(o => o.Status == PrintOrderStatus.Cancelled);

            Orders = StatusFilter?.ToLowerInvariant() switch
            {
                "pending" => allOrders.Where(o => o.Status == PrintOrderStatus.Pending).ToList(),
                "processing" => allOrders.Where(o => o.Status == PrintOrderStatus.Processing || o.Status == PrintOrderStatus.Printing || o.Status == PrintOrderStatus.ShelfAssigned).ToList(),
                "ready" => allOrders.Where(o => o.Status == PrintOrderStatus.ReadyForPickup).ToList(),
                "completed" => allOrders.Where(o => o.Status == PrintOrderStatus.Completed).ToList(),
                "cancelled" => allOrders.Where(o => o.Status == PrintOrderStatus.Cancelled).ToList(),
                _ => allOrders
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid orderId)
    {
        var studentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(studentIdStr, out var studentId))
        {
            return RedirectToPage("/Auth/Login");
        }

        var result = await _orderService.CancelOrderAsync(orderId, studentId);
        if (result.Success)
        {
            SuccessMessage = "Đã hủy đơn hàng thành công.";
        }
        else
        {
            ErrorMessage = result.Message ?? "Không thể hủy đơn hàng.";
        }

        return RedirectToPage(new { StatusFilter });
    }
}
