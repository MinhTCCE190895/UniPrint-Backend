using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.Staff;

[Authorize(Roles = "Staff,Admin")]
public class ShelvesModel : PageModel
{
    private readonly WebApiClient _api;

    public ShelvesModel(WebApiClient api)
    {
        _api = api;
    }

    public ShelfInventoryResponseDto Inventory { get; set; } = new();
    public List<ShelfInventoryItemDto> Shelves => Inventory.Shelves;
    public ShelfInventorySummaryDto Summary => Inventory.Summary;

    [BindProperty]
    public CreateShelfDto CreateInput { get; set; } = new();

    [BindProperty]
    public UpdateShelfDto UpdateInput { get; set; } = new();

    [BindProperty]
    public Guid EditShelfId { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var response = await _api.GetAsync<ApiResponse<ShelfInventoryResponseDto>>("api/shelves/inventory");
        Inventory = response?.Data ?? new ShelfInventoryResponseDto();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(CreateInput.ShelfCode))
        {
            ErrorMessage = "Vui lòng nhập mã kệ (ví dụ: A1, A2, B1...).";
            return RedirectToPage();
        }

        var result = await _api.PostAsync<ApiResponse<ShelfResponseDto>>("api/shelves", CreateInput);
        if (result == null || !result.Success)
        {
            ErrorMessage = result?.Message ?? "Tạo kệ thất bại.";
        }
        else
        {
            SuccessMessage = result.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        if (EditShelfId == Guid.Empty)
        {
            ErrorMessage = "Kệ cần cập nhật không hợp lệ.";
            return RedirectToPage();
        }

        var result = await _api.PutAsync<ApiResponse<ShelfResponseDto>>($"api/shelves/{EditShelfId}", UpdateInput);
        if (result == null || !result.Success)
        {
            ErrorMessage = result?.Message ?? "Cập nhật kệ thất bại.";
        }
        else
        {
            SuccessMessage = result.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            ErrorMessage = "ID kệ không hợp lệ.";
            return RedirectToPage();
        }

        var result = await _api.DeleteAsync<ApiResponse<bool>>($"api/shelves/{id}");
        if (result == null || !result.Success)
        {
            ErrorMessage = result?.Message ?? "Xóa kệ thất bại.";
        }
        else
        {
            SuccessMessage = result.Message;
        }

        return RedirectToPage();
    }
}
