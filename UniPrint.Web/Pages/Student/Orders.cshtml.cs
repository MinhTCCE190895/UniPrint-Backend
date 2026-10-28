using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.Web.Pages.Student;

/// <summary>Task 19: Danh sách đơn in của sinh viên, mỗi đơn dẫn tới trang theo dõi chi tiết.</summary>
public class OrdersModel : PageModel
{
    private readonly IPrintOrderService _orderService;

    public OrdersModel(IPrintOrderService orderService)
    {
        _orderService = orderService;
    }

    public List<PrintOrderResponseDto> Orders { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var studentId))
            return RedirectToPage("/Auth/Login");

        var result = await _orderService.GetStudentOrdersAsync(studentId);
        Orders = result.Data ?? new();
        return Page();
    }
}
