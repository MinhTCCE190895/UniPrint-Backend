using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages;

public class IndexModel : PageModel
{
    private readonly WebApiClient _api;

    public IndexModel(WebApiClient api)
    {
        _api = api;
    }

    public List<PrintOrderResponseDto> RecentOrders { get; set; } = new();
    public int TotalDocuments { get; set; } = 0;

    public async Task OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Student"))
        {
            var ordersResult = await _api.GetAsync<ApiResponse<List<PrintOrderResponseDto>>>("api/printorders/my-orders");
            if (ordersResult != null && ordersResult.Success && ordersResult.Data != null)
            {
                RecentOrders = ordersResult.Data.Take(4).ToList();
            }

            var docsResult = await _api.GetAsync<ApiResponse<List<DocumentResponseDto>>>("api/documents/my-documents");
            if (docsResult != null && docsResult.Success && docsResult.Data != null)
            {
                TotalDocuments = docsResult.Data.Count;
            }
        }
    }
}
