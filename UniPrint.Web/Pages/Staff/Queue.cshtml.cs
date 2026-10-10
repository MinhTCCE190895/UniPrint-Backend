using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Enums;

namespace UniPrint.Web.Pages.Staff;

public class QueueModel : PageModel
{
    private readonly IPrintOrderService _orderService;

    public QueueModel(IPrintOrderService orderService)
    {
        _orderService = orderService;
    }

    public List<PrintOrderResponseDto> Orders { get; set; } = new();

    public async Task OnGetAsync()
    {
        var result = await _orderService.GetStaffQueueOrdersAsync();
        Orders = result.Data ?? new List<PrintOrderResponseDto>();
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(Guid orderId, PrintOrderStatus newStatus)
    {
        var staffIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var staffId = staffIdClaim != null ? Guid.Parse(staffIdClaim) : Guid.Parse("22222222-2222-2222-2222-222222222222");
        await _orderService.UpdateOrderStatusAsync(orderId, staffId, newStatus);
        return RedirectToPage();
    }
}
