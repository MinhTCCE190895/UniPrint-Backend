using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.Web.Pages.Student;

[Authorize(Roles = "Student")]
public class DocumentsModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly IWebHostEnvironment _environment;

    public DocumentsModel(IDocumentService documentService, IWebHostEnvironment environment)
    {
        _documentService = documentService;
        _environment = environment;
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
        var studentId = GetCurrentUserId();
        if (studentId == null) return RedirectToPage("/Auth/Login");

        var result = await _documentService.GetMyDocumentsAsync(studentId.Value);
        if (result.Success && result.Data != null)
        {
            Documents = result.Data;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync()
    {
        var studentId = GetCurrentUserId();
        if (studentId == null) return RedirectToPage("/Auth/Login");

        if (UploadedFile == null || UploadedFile.Length == 0)
        {
            ErrorMessage = "Vui lòng chọn file tài liệu PDF hoặc DOCX để tải lên.";
            return RedirectToPage();
        }

        var storageFolder = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "uploads", "documents");

        using var stream = UploadedFile.OpenReadStream();
        var result = await _documentService.UploadDocumentStreamAsync(
            studentId.Value,
            stream,
            UploadedFile.FileName,
            UploadedFile.Length,
            storageFolder);

        if (result.Success)
        {
            SuccessMessage = result.Message ?? "Tải tài liệu lên thành công!";
        }
        else
        {
            ErrorMessage = result.Message ?? "Tải tài liệu thất bại.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid documentId)
    {
        var studentId = GetCurrentUserId();
        if (studentId == null) return RedirectToPage("/Auth/Login");

        var result = await _documentService.DeleteDocumentAsync(documentId, studentId.Value);
        if (result.Success)
        {
            SuccessMessage = result.Message ?? "Đã xóa tài liệu thành công.";
        }
        else
        {
            ErrorMessage = result.Message ?? "Không thể xóa tài liệu.";
        }

        return RedirectToPage();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdStr, out var id) ? id : null;
    }
}
