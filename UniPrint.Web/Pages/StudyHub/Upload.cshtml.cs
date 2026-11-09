
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Context;

namespace UniPrint.Web.Pages.StudyHub;

[Authorize(Roles = "Student")]
public class UploadModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly IStudyHubService _studyHubService;
    private readonly UniPrintDbContext _dbContext;
    private readonly ILogger<UploadModel> _logger;

    public UploadModel(
        IDocumentService documentService,
        IStudyHubService studyHubService,
        UniPrintDbContext dbContext,
        ILogger<UploadModel> logger)
    {
        _documentService = documentService;
        _studyHubService = studyHubService;
        _dbContext = dbContext;
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
        Subjects = await _dbContext.Subjects
            .AsNoTracking()
            .OrderBy(s => s.Code)
            .Select(s => new SubjectOption
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name
            })
            .ToListAsync();

        Categories = await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryOption
            {
                Id = c.Id,
                Name = c.Name
            })
            .ToListAsync();
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

        // 1. Validate form
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (SubjectId == Guid.Empty ||
            CategoryId == Guid.Empty)
        {
            ModelState.AddModelError(
                "",
                "Vui lòng chọn môn học và danh mục."
            );

            return Page();
        }

        // 2. Validate file
        if (UploadedFile == null ||
            UploadedFile.Length == 0)
        {
            ModelState.AddModelError(
                "",
                "Vui lòng chọn file tài liệu."
            );

            return Page();
        }

        var extension = Path
            .GetExtension(UploadedFile.FileName)
            .ToLowerInvariant();

        if (extension != ".pdf" &&
            extension != ".docx")
        {
            ModelState.AddModelError(
                "",
                "Chỉ hỗ trợ file PDF hoặc DOCX."
            );

            return Page();
        }

        const long maxFileSize = 10 * 1024 * 1024;

        if (UploadedFile.Length > maxFileSize)
        {
            ModelState.AddModelError(
                "",
                "Dung lượng file tối đa là 10MB."
            );

            return Page();
        }

        // 3. Get student ID from Cookie Authentication
        var userIdString = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdString, out var studentId))
        {
            return Challenge();
        }

        try
        {
            // 4. Validate selected subject and category
            var subjectExists = await _dbContext.Subjects
                .AnyAsync(s => s.Id == SubjectId);

            var categoryExists = await _dbContext.Categories
                .AnyAsync(c => c.Id == CategoryId);

            if (!subjectExists || !categoryExists)
            {
                ModelState.AddModelError(
                    "",
                    "Môn học hoặc danh mục không hợp lệ."
                );

                return Page();
            }

            // 5. Upload document
            await using var stream =
                UploadedFile.OpenReadStream();

            var documentResult =
                await _documentService.UploadDocumentStreamAsync(
                    studentId,
                    stream,
                    UploadedFile.FileName,
                    UploadedFile.Length
                );

            if (!documentResult.Success ||
                documentResult.Data == null)
            {
                Message = documentResult.Message
                    ?? "Không thể tải file lên hệ thống.";

                return Page();
            }

            // 6. Create Study Material
            var request = new CreateMaterialDto
            {
                Title = Title,
                Description = Description,
                DocumentId = documentResult.Data.Id,
                SubjectId = SubjectId,
                CategoryId = CategoryId
            };

            var materialResult =
                await _studyHubService.UploadMaterialAsync(
                    studentId,
                    request
                );

            if (!materialResult.Success)
            {
                Message =
                    "File đã tải lên nhưng không thể " +
                    "tạo Study Material: " +
                    materialResult.Message;

                return Page();
            }

            // 7. Success
            TempData["SuccessMessage"] =
                "Đăng tải thành công! " +
                "Tài liệu đang chờ Admin phê duyệt.";

            return RedirectToPage("/StudyHub/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Upload Study Material failed for student {StudentId}",
                studentId
            );

            Message =
                "Có lỗi xảy ra khi đăng tải tài liệu. " +
                "Vui lòng thử lại.";

            return Page();
        }
    }
}
