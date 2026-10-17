using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.Web.Pages.Staff;

public class ShelvesModel : PageModel
{
    private readonly IShelfService _shelfService;

    public ShelvesModel(IShelfService shelfService)
    {
        _shelfService = shelfService;
    }

    public List<ShelfResponseDto> Shelves { get; set; } = new();

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
        var response = await _shelfService.GetAllShelvesAsync();
        Shelves = response.Data ?? new List<ShelfResponseDto>();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(CreateInput.ShelfCode))
        {
            ErrorMessage = "Vui lòng nhập mã kệ (ví dụ: A1, A2, B1...).";
            return RedirectToPage();
        }

        var result = await _shelfService.CreateShelfAsync(CreateInput);
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

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        if (EditShelfId == Guid.Empty)
        {
            ErrorMessage = "Kệ cần cập nhật không hợp lệ.";
            return RedirectToPage();
        }

        var result = await _shelfService.UpdateShelfAsync(EditShelfId, UpdateInput);
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

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            ErrorMessage = "ID kệ không hợp lệ.";
            return RedirectToPage();
        }

        var result = await _shelfService.DeleteShelfAsync(id);
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
}
