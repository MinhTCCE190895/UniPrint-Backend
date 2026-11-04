
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Enums;

namespace UniPrint.Web.Pages.StudyHub;

public class DetailsModel : PageModel
{
    private readonly UniPrintDbContext _dbContext;
    private readonly IStudyHubService _studyHubService;

    public DetailsModel(
        UniPrintDbContext dbContext,
        IStudyHubService studyHubService)
    {
        _dbContext = dbContext;
        _studyHubService = studyHubService;
    }

    public StudyMaterialDto Material { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public List<ReviewItem> Reviews { get; set; } = new();

    public bool HasReviewed { get; set; }

    [BindProperty]
    [Range(1, 5, ErrorMessage = "Vui lòng chọn từ 1 đến 5 sao.")]
    public int Rating { get; set; }

    [BindProperty]
    [StringLength(1000, ErrorMessage = "Nhận xét tối đa 1000 ký tự.")]
    public string? Comment { get; set; }

    public class ReviewItem
    {
        public Guid StudentId { get; set; }
        public string StudentName { get; set; } = "";
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }

    private async Task<bool> LoadMaterialAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var material = await _dbContext.StudyMaterials
            .AsNoTracking()
            .Where(m =>
                m.Id == id &&
                m.Status == MaterialStatus.Approved)
            .Select(m => new StudyMaterialDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                DocumentId = m.DocumentId,
                DocumentFileName = m.Document.FileName,
                SubjectCode = m.Subject.Code,
                SubjectName = m.Subject.Name,
                CategoryName = m.Category.Name,
                UploaderName = m.UploadedByStudent.FullName,
                Status = m.Status,
                DownloadCount = m.DownloadCount,
                AverageRating = m.AverageRating,
                CreatedAt = m.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (material == null)
            return false;

        Material = material;

        Reviews = await (
            from review in _dbContext.Set<
                UniPrint.DataAccess.Entities.MaterialReview>()
                .AsNoTracking()
            join student in _dbContext.Users.AsNoTracking()
                on review.StudentId equals student.Id
            where review.MaterialId == id
            orderby review.CreatedAt descending
            select new ReviewItem
            {
                StudentId = review.StudentId,
                StudentName = student.FullName,
                Rating = review.Rating,
                Comment = review.Comment
            }
        ).ToListAsync(ct);

        return true;
    }

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        CancellationToken ct)
    {
        if (!await LoadMaterialAsync(id, ct))
            return NotFound();

        // Lấy đánh giá cũ của sinh viên đang đăng nhập
        if (User.IsInRole("Student"))
        {
            var userIdString = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (Guid.TryParse(userIdString, out var studentId))
            {
                var existing = Reviews.FirstOrDefault(
                    r => r.StudentId == studentId);

                if (existing != null)
                {
                    HasReviewed = true;
                    Rating = existing.Rating;
                    Comment = existing.Comment;
                }
            }
        }

        return Page();
    }

    // ========================================
    // CREATE / UPDATE REVIEW - BR05
    // ========================================

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> OnPostReviewAsync(
        Guid id,
        CancellationToken ct)
    {
        if (!await LoadMaterialAsync(id, ct))
            return NotFound();

        if (!ModelState.IsValid)
            return Page();

        var userIdString = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdString, out var studentId))
            return Challenge();

        var reviewDto = new MaterialReviewDto
        {
            Rating = Rating,
            Comment = Comment?.Trim()
        };

        var result = await _studyHubService.AddReviewAsync(
            id,
            studentId,
            reviewDto,
            ct);

        if (!result.Success)
        {
            ErrorMessage = result.Message
                ?? "Không thể lưu đánh giá.";

            var existing = Reviews.FirstOrDefault(
                r => r.StudentId == studentId);

            HasReviewed = existing != null;

            return Page();
        }

        TempData["ReviewSuccess"] = result.Message
            ?? "Đã lưu đánh giá thành công!";

        return RedirectToPage(
            "/StudyHub/Details",
            new { id });
    }

    // ========================================
    // DOWNLOAD STUDY MATERIAL
    // ========================================

    public async Task<IActionResult> OnGetDownloadAsync(
        Guid id,
        CancellationToken ct)
    {
        // 1. Chỉ cho tải tài liệu Approved
        var material = await _dbContext.StudyMaterials
            .AsNoTracking()
            .Where(m =>
                m.Id == id &&
                m.Status == MaterialStatus.Approved)
            .Select(m => new
            {
                m.Id,
                m.DocumentId
            })
            .FirstOrDefaultAsync(ct);

        if (material == null)
            return NotFound(
                "Tài liệu không tồn tại hoặc chưa được duyệt.");

        // 2. Lấy file vật lý
        var document = await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.Id == material.DocumentId)
            .Select(d => new
            {
                d.FilePath,
                d.FileName,
                d.FileExtension
            })
            .FirstOrDefaultAsync(ct);

        if (document == null ||
            string.IsNullOrWhiteSpace(document.FilePath))
        {
            return NotFound("Không tìm thấy thông tin file.");
        }

        if (!System.IO.File.Exists(document.FilePath))
        {
            return NotFound(
                "File không còn tồn tại trên máy chủ.");
        }

        // 3. Kiểm tra định dạng file
        var extension = Path.GetExtension(document.FileName)
            .ToLowerInvariant();

        if (extension != ".pdf" && extension != ".docx")
        {
            return BadRequest(
                "Định dạng tài liệu không được hỗ trợ.");
        }

        // 4. Mở file
        FileStream stream;

        try
        {
            stream = new FileStream(
                document.FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
        }
        catch (IOException)
        {
            return StatusCode(
                500, "Không thể đọc file tài liệu.");
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(
                500, "Không có quyền đọc file.");
        }

        // 5. Tăng lượt tải
        try
        {
            var result = await _studyHubService
                .IncrementDownloadCountAsync(material.Id, ct);

            if (!result.Success)
            {
                await stream.DisposeAsync();
                return BadRequest(result.Message);
            }
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }

        // 6. Trả file về trình duyệt
        var contentType = extension == ".pdf"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        return File(
            stream,
            contentType,
            Path.GetFileName(document.FileName));
    }
}
