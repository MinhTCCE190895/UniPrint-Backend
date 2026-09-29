using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Services;

namespace UniPrint.Web.Pages.Pickup;

public class ScanQRModel : PageModel
{
    private readonly IPrintOrderService _orderService;

    public ScanQRModel(IPrintOrderService orderService)
    {
        _orderService = orderService;
    }

    [BindProperty]
    public string QrToken { get; set; } = string.Empty;

    public string? Message { get; set; }
    public bool IsSuccess { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var staffId = Guid.Parse("22222222-2222-2222-2222-222222222222"); // Staff Demo
        var result = await _orderService.ScanPickupQrAsync(QrToken.Trim(), staffId);

        IsSuccess = result.Success;
        Message = result.Message;

        return Page();
    }
}
