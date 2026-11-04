
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Enums;

namespace UniPrint.Web.Pages.StudyHub;

public class IndexModel : PageModel
{
    private readonly IStudyHubService _studyHubService;
    private readonly UniPrintDbContext _dbContext;

    public IndexModel(
        IStudyHubService studyHubService,
        UniPrintDbContext dbContext)
    {
        _studyHubService = studyHubService;
        _dbContext = dbContext;
    }

    // ==========================================
    // 1. DANH SÁCH TÀI LIỆU
    // ==========================================

    public List<StudyMaterialDto> Materials { get; set; } = new();

    public List<MyMaterialItem> MyMaterials { get; set; } = new();

    // ==========================================
    // 2. SEARCH & FILTER - GET PARAMETERS
    // ==========================================

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? SubjectId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? CategoryId { get; set; }

    // ==========================================
    // 3. DROPDOWN OPTIONS
    // ==========================================

    public List<SelectListItem> SubjectOptions { get; set; } = new();

    public List<SelectListItem> CategoryOptions { get; set; } = new();

    // ==========================================
    // 4. DTO TÀI LIỆU CỦA SINH VIÊN
    // ==========================================

    public class MyMaterialItem
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public MaterialStatus Status { get; set; }

        public string SubjectCode { get; set; } = "";

        public string SubjectName { get; set; } = "";

        public string CategoryName { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }

    // ==========================================
    // 5. LOAD DROPDOWN DATA
    // ==========================================

    private async Task LoadFilterOptionsAsync()
    {
        SubjectOptions = await _dbContext.Subjects
            .AsNoTracking()
            .OrderBy(s => s.Code)
            .Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = s.Code + " - " + s.Name
            })
            .ToListAsync();

        CategoryOptions = await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();

        // Giữ lựa chọn sau khi tìm kiếm
        foreach (var option in SubjectOptions)
        {
            option.Selected =
                SubjectId.HasValue &&
                option.Value == SubjectId.Value.ToString();
        }

        foreach (var option in CategoryOptions)
        {
            option.Selected =
                CategoryId.HasValue &&
                option.Value == CategoryId.Value.ToString();
        }
    }

    // ==========================================
    // 6. GET STUDY HUB
    // ==========================================

    public async Task OnGetAsync()
    {
        // Bước 1: Load môn học và danh mục
        await LoadFilterOptionsAsync();

        // Bước 2: Search & Filter tài liệu Approved
        var result = await _studyHubService
            .GetApprovedMaterialsAsync(
                Search?.Trim(),
                SubjectId,
                CategoryId
            );

        Materials = result.Data ?? new List<StudyMaterialDto>();

        // Bước 3: EC08 - Chỉ hiển thị tài liệu Approved
        // Truy vấn trực tiếp theo Id để kiểm tra trạng thái
        // ngay cả khi Business Service trả về dữ liệu khác.
        var approvedIds = await _dbContext.StudyMaterials
            .AsNoTracking()
            .Where(m => m.Status == MaterialStatus.Approved)
            .Select(m => m.Id)
            .ToListAsync();

        var approvedIdSet = approvedIds.ToHashSet();

        Materials = Materials
            .Where(m => approvedIdSet.Contains(m.Id))
            .ToList();

        // Bước 4: Chỉ Student mới xem tài liệu cá nhân
        if (!User.IsInRole("Student"))
            return;

        var userIdString = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdString, out var studentId))
            return;

        // Bước 5: Lấy tất cả tài liệu của Student
        // Bao gồm Pending, Approved, Rejected
        MyMaterials = await (
            from material in _dbContext.StudyMaterials.AsNoTracking()

            join document in _dbContext.Documents.AsNoTracking()
                on material.DocumentId equals document.Id

            join subject in _dbContext.Subjects.AsNoTracking()
                on material.SubjectId equals subject.Id

            join category in _dbContext.Categories.AsNoTracking()
                on material.CategoryId equals category.Id

            where document.UserId == studentId

            orderby material.CreatedAt descending

            select new MyMaterialItem
            {
                Id = material.Id,
                Title = material.Title,
                Description = material.Description,
                Status = material.Status,
                SubjectCode = subject.Code,
                SubjectName = subject.Name,
                CategoryName = category.Name,
                CreatedAt = material.CreatedAt
            }
        ).ToListAsync();
    }
}
