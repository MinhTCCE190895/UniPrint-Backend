using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Enums;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.Staff;

[Authorize(Roles = "Staff,Admin")]
public class QueueModel : PageModel
{
    private readonly WebApiClient _api;

    public QueueModel(WebApiClient api)
    {
        _api = api;
    }

    public List<PrintOrderResponseDto> Orders { get; set; } = new();
    public List<ShelfResponseDto> AvailableShelves { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var result = await _api.GetAsync<ApiResponse<List<PrintOrderResponseDto>>>("api/printorders/queue");
        Orders = result?.Data ?? new List<PrintOrderResponseDto>();

        var shelvesResult = await _api.GetAsync<ApiResponse<List<ShelfResponseDto>>>("api/shelves/available");
        AvailableShelves = shelvesResult?.Data ?? new List<ShelfResponseDto>();
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(Guid orderId, PrintOrderStatus newStatus)
    {
        var result = await _api.PatchAsync<ApiResponse<bool>>($"api/printorders/{orderId}/status", new UpdateOrderStatusDto
        {
            NewStatus = newStatus
        });

        if (result != null && result.Success)
        {
            SuccessMessage = result.Message;
        }
        else
        {
            ErrorMessage = result?.Message ?? "Cập nhật trạng thái thất bại.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAssignShelfAsync(Guid orderId, Guid shelfId)
    {
        var result = await _api.PostAsync<ApiResponse<bool>>($"api/printorders/{orderId}/assign-shelf", new AssignShelfDto
        {
            ShelfId = shelfId
        });

        if (result != null && result.Success)
        {
            SuccessMessage = result.Message;
        }
        else
        {
            ErrorMessage = result?.Message ?? "Gán kệ thất bại.";
        }
        return RedirectToPage();
    }
}
