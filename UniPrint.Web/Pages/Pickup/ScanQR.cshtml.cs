using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using UniPrint.Business.Services;

namespace UniPrint.Web.Pages.Pickup;

[Authorize(Roles = "Staff,Admin")]
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
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId))
            return Challenge();
        var result = await _orderService.ScanPickupQrAsync(QrToken.Trim(), staffId);

        IsSuccess = result.Success;
        Message = result.Message;

        return Page();
    }
}
