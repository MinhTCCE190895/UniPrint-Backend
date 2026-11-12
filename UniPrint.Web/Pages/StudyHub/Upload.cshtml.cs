using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.StudyHub;

[Authorize(Roles = "Student")]
public class UploadModel : PageModel
{
    private readonly WebApiClient _api;
    private readonly ILogger<UploadModel> _logger;

    public UploadModel(WebApiClient api, ILogger<UploadModel> logger)
    {
        _api = api;
        _logger = logger;
    }

    // ================================
    // FORM INPUT
    // ================================

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng nhập tên tài liệu.")]
    public string Title { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng nhập mô tả tài liệu.")]
    public string Description { get; set; } = string.Empty;

    [BindProperty]
    public Guid SubjectId { get; set; }

    [BindProperty]
    public Guid CategoryId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng chọn file.")]
    public IFormFile? UploadedFile { get; set; }

    public string? Message { get; set; }

    // ================================
    // DROPDOWN OPTIONS
    // ================================

    public List<SubjectOption> Subjects { get; set; } = new();
    public List<CategoryOption> Categories { get; set; } = new();

    public class SubjectOption
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class CategoryOption
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }

    // ================================
    // LOAD SUBJECTS & CATEGORIES
    // ================================

    private async Task LoadOptionsAsync()
    {
        var subRes = await _api.GetAsync<ApiResponse<List<SubjectOption>>>("api/lookup/subjects");
        Subjects = subRes?.Data ?? new();

        var catRes = await _api.GetAsync<ApiResponse<List<CategoryOption>>>("api/lookup/categories");
        Categories = catRes?.Data ?? new();
    }

    // ================================
    // GET: UPLOAD PAGE
    // ================================

    public async Task OnGetAsync()
    {
        await LoadOptionsAsync();
    }

    // ================================
    // POST: UPLOAD MATERIAL
    // ================================

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadOptionsAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (SubjectId == Guid.Empty || CategoryId == Guid.Empty)
        {
            ModelState.AddModelError("", "Vui lòng chọn môn học và danh mục.");
            return Page();
        }

        if (UploadedFile == null || UploadedFile.Length == 0)
        {
            ModelState.AddModelError("", "Vui lòng chọn file tài liệu.");
            return Page();
        }

        var extension = Path.GetExtension(UploadedFile.FileName).ToLowerInvariant();
        if (extension != ".pdf" && extension != ".docx")
        {
            ModelState.AddModelError("", "Chỉ hỗ trợ file PDF hoặc DOCX.");
            return Page();
        }

        const long maxFileSize = 10 * 1024 * 1024;
        if (UploadedFile.Length > maxFileSize)
        {
            ModelState.AddModelError("", "Dung lượng file tối đa là 10MB.");
            return Page();
        }

        try
        {
            // 1. Upload file lên API DocumentsController
            using var form = new MultipartFormDataContent();
            using var stream = UploadedFile.OpenReadStream();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                UploadedFile.ContentType ?? "application/octet-stream");
            form.Add(fileContent, "file", UploadedFile.FileName);

            var docResult = await _api.PostMultipartAsync<ApiResponse<DocumentResponseDto>>("api/documents/upload", form);
            if (docResult == null || !docResult.Success || docResult.Data == null)
            {
                ModelState.AddModelError("", docResult?.Message ?? "Có lỗi khi tải file lên.");
                return Page();
            }

            // 2. Tạo Study Material qua API StudyHubController
            var materialResult = await _api.PostAsync<ApiResponse<StudyMaterialDto>>("api/studyhub/materials", new CreateMaterialDto
            {
                Title = Title.Trim(),
                Description = Description.Trim(),
                DocumentId = docResult.Data.Id,
                SubjectId = SubjectId,
                CategoryId = CategoryId
            });

            if (materialResult == null || !materialResult.Success)
            {
                ModelState.AddModelError("", materialResult?.Message ?? "Có lỗi khi đăng tải tài liệu.");
                return Page();
            }

            TempData["SuccessMessage"] = "Tài liệu học tập đã được gửi tới quản trị viên để chờ phê duyệt!";
            return RedirectToPage("/StudyHub/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading study material");
            ModelState.AddModelError("", "Có lỗi xảy ra trong quá trình xử lý.");
            return Page();
        }
    }
}
