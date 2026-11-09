using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Enums;

namespace UniPrint.API.Controllers;

/// <summary>
/// Lấy danh sách Subject, Category và chi tiết tài liệu cho StudyHub
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LookupController : ControllerBase
{
    private readonly UniPrintDbContext _dbContext;

    public LookupController(UniPrintDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Lấy danh sách tất cả môn học</summary>
    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects(CancellationToken ct)
    {
        var subjects = await _dbContext.Subjects
            .AsNoTracking()
            .OrderBy(s => s.Code)
            .Select(s => new { s.Id, s.Code, s.Name })
            .ToListAsync(ct);
        return Ok(new { success = true, data = subjects });
    }

    /// <summary>Lấy danh sách tất cả danh mục tài liệu</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Description })
            .ToListAsync(ct);
        return Ok(new { success = true, data = categories });
    }

    /// <summary>Lấy chi tiết 1 tài liệu học tập đã duyệt theo Id</summary>
    [HttpGet("study-material/{id:guid}")]
    public async Task<IActionResult> GetStudyMaterialById(Guid id, CancellationToken ct)
    {
        var material = await _dbContext.StudyMaterials
            .AsNoTracking()
            .Where(m => m.Id == id && m.Status == MaterialStatus.Approved)
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

        if (material == null) return NotFound(new { success = false, message = "Không tìm thấy tài liệu." });
        return Ok(new { success = true, data = material });
    }

    /// <summary>Lấy danh sách reviews của tài liệu, dùng cho trang Details</summary>
    [HttpGet("study-material-reviews/{materialId:guid}")]
    public async Task<IActionResult> GetMaterialReviews(Guid materialId, CancellationToken ct)
    {
        var reviews = await _dbContext.MaterialReviews
            .AsNoTracking()
            .Where(r => r.MaterialId == materialId)
            .Join(_dbContext.Users.AsNoTracking(),
                r => r.StudentId,
                u => u.Id,
                (r, u) => new
                {
                    r.StudentId,
                    StudentName = u.FullName,
                    r.Rating,
                    r.Comment
                })
            .OrderByDescending(r => r.Rating)
            .ToListAsync(ct);

        return Ok(new { success = true, data = reviews });
    }

    /// <summary>Tải file của tài liệu học tập và tự động tăng download count</summary>
    [HttpGet("study-material/{id:guid}/download")]
    public async Task<IActionResult> DownloadStudyMaterial(Guid id, CancellationToken ct)
    {
        var material = await _dbContext.StudyMaterials
            .Include(m => m.Document)
            .FirstOrDefaultAsync(m => m.Id == id && m.Status == MaterialStatus.Approved, ct);

        if (material == null || material.Document == null)
            return NotFound("Tài liệu không tồn tại hoặc chưa được duyệt.");

        var filePath = material.Document.FilePath;
        if (!System.IO.File.Exists(filePath))
            return NotFound("File không còn tồn tại trên máy chủ.");

        material.DownloadCount++;
        await _dbContext.SaveChangesAsync(ct);

        var contentType = material.Document.FileExtension.ToLower() == ".pdf"
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath, ct);
        return File(fileBytes, contentType, material.Document.FileName);
    }

    /// <summary>Lấy danh sách tài liệu của sinh viên hiện tại (kể cả Pending, Rejected)</summary>
    [HttpGet("my-materials")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyMaterials(CancellationToken ct)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var studentId))
            return Unauthorized();

        var materials = await (
            from material in _dbContext.StudyMaterials.AsNoTracking()
            join document in _dbContext.Documents.AsNoTracking()
                on material.DocumentId equals document.Id
            join subject in _dbContext.Subjects.AsNoTracking()
                on material.SubjectId equals subject.Id
            join category in _dbContext.Categories.AsNoTracking()
                on material.CategoryId equals category.Id
            where document.UserId == studentId
            orderby material.CreatedAt descending
            select new
            {
                material.Id,
                material.Title,
                material.Description,
                material.Status,
                SubjectCode = subject.Code,
                SubjectName = subject.Name,
                CategoryName = category.Name,
                material.CreatedAt
            }
        ).ToListAsync(ct);

        return Ok(new { success = true, data = materials });
    }
}
