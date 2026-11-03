using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IPrintOrderService _orderService;
    private readonly IDocumentService _documentService;

    public IndexModel(IPrintOrderService orderService, IDocumentService documentService)
    {
        _orderService = orderService;
        _documentService = documentService;
    }

    // [UI Presentation Only] Dữ liệu theo dõi tiến độ và tóm tắt kho tài liệu
    public List<PrintOrderResponseDto> RecentOrders { get; set; } = new();
    public int TotalDocuments { get; set; } = 0;

    public async Task OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Student"))
        {
            if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                var ordersResult = await _orderService.GetStudentOrdersAsync(userId);
                if (ordersResult.Success && ordersResult.Data != null)
                {
                    // Lấy 4 đơn in gần nhất để hiển thị theo dõi tiến độ trên trang chủ
                    RecentOrders = ordersResult.Data.Take(4).ToList();
                }

                var docsResult = await _documentService.GetMyDocumentsAsync(userId);
                if (docsResult.Success && docsResult.Data != null)
                {
                    TotalDocuments = docsResult.Data.Count;
                }
            }
        }
    }
}
