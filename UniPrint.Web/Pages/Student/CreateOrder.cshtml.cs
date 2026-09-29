using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.Web.Pages.Student;

public class CreateOrderModel : PageModel
{
    private readonly IPriceCalculatorService _priceCalculator;
    private readonly IPrintOrderService _orderService;

    public CreateOrderModel(IPriceCalculatorService priceCalculator, IPrintOrderService orderService)
    {
        _priceCalculator = priceCalculator;
        _orderService = orderService;
    }

    [BindProperty]
    public int PageCount { get; set; } = 10;

    [BindProperty]
    public int NumberOfCopies { get; set; } = 1;

    [BindProperty]
    public bool IsColor { get; set; }

    [BindProperty]
    public bool IsDoubleSided { get; set; } = true;

    [BindProperty]
    public bool HasBinding { get; set; }

    [BindProperty]
    public bool HasStaple { get; set; } = true;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var studentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(studentIdStr, out var studentId))
        {
            return RedirectToPage("/Auth/Login");
        }

        // Logic demo tạo đơn
        return RedirectToPage("/Student/MyOrders");
    }
}
