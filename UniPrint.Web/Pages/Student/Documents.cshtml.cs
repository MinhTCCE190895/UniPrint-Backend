using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.Student;

[Authorize(Roles = "Student")]
public class DocumentsModel : PageModel
{
    private readonly WebApiClient _api;

    public DocumentsModel(WebApiClient api)
    {
        _api = api;
    }

    public List<DocumentResponseDto> Documents { get; set; } = new();

    [BindProperty]
    public IFormFile? UploadedFile { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var result = await _api.GetAsync<ApiResponse<List<DocumentResponseDto>>>("api/documents/my-documents");
        if (result != null && result.Success && result.Data != null)
        {
            Documents = result.Data;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync()
    {
        if (UploadedFile == null || UploadedFile.Length == 0)
        {
            ErrorMessage = "Vui lòng chọn file tài liệu PDF hoặc DOCX để tải lên.";
            return RedirectToPage();
        }

        using var form = new MultipartFormDataContent();
        using var stream = UploadedFile.OpenReadStream();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            UploadedFile.ContentType ?? "application/octet-stream");
        form.Add(fileContent, "file", UploadedFile.FileName);

        var result = await _api.PostMultipartAsync<ApiResponse<DocumentResponseDto>>("api/documents/upload", form);

        if (result != null && result.Success)
        {
            SuccessMessage = result.Message ?? "Tải tài liệu lên thành công!";
        }
        else
        {
            ErrorMessage = result?.Message ?? "Tải tài liệu thất bại.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid documentId)
    {
        var result = await _api.DeleteAsync<ApiResponse<bool>>($"api/documents/{documentId}");
        if (result != null && result.Success)
        {
            SuccessMessage = result.Message ?? "Đã xóa tài liệu thành công.";
        }
        else
        {
            ErrorMessage = result?.Message ?? "Không thể xóa tài liệu.";
        }

        return RedirectToPage();
    }
}
