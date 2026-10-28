using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Enums;

namespace UniPrint.Web.Pages.Staff;

public class QueueModel : PageModel
{
    private readonly IPrintOrderService _orderService;
    private readonly IShelfService _shelfService;

    public QueueModel(IPrintOrderService orderService, IShelfService shelfService)
    {
        _orderService = orderService;
        _shelfService = shelfService;
    }

    public List<PrintOrderResponseDto> Orders { get; set; } = new();
    public List<ShelfResponseDto> AvailableShelves { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var result = await _orderService.GetStaffQueueOrdersAsync();
        Orders = result.Data ?? new List<PrintOrderResponseDto>();

        var shelvesResult = await _shelfService.GetAvailableShelvesAsync();
        AvailableShelves = shelvesResult.Data ?? new List<ShelfResponseDto>();
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(Guid orderId, PrintOrderStatus newStatus)
    {
        var staffId = GetCurrentStaffId();
        var result = await _orderService.UpdateOrderStatusAsync(orderId, staffId, newStatus);
        if (!result.Success)
        {
            ErrorMessage = result.Message;
        }
        else
        {
            SuccessMessage = result.Message;
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAssignShelfAsync(Guid orderId, Guid shelfId)
    {
        var staffId = GetCurrentStaffId();
        var result = await _orderService.AssignShelfAsync(orderId, staffId, shelfId);
        if (!result.Success)
        {
            ErrorMessage = result.Message;
        }
        else
        {
            SuccessMessage = result.Message;
        }
        return RedirectToPage();
    }

    private Guid GetCurrentStaffId()
    {
        var staffIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return staffIdClaim != null ? Guid.Parse(staffIdClaim) : Guid.Parse("22222222-2222-2222-2222-222222222222");
    }
}
