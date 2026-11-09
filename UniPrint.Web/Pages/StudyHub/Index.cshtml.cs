using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Enums;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.StudyHub;

public class IndexModel : PageModel
{
    private readonly WebApiClient _api;

    public IndexModel(WebApiClient api)
    {
        _api = api;
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

    private class LookupSubject
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private class LookupCategory
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
    }

    // ==========================================
    // 5. LOAD DROPDOWN DATA
    // ==========================================

    private async Task LoadFilterOptionsAsync()
    {
        var subjectsRes = await _api.GetAsync<ApiResponse<List<LookupSubject>>>("api/lookup/subjects");
        var subjects = subjectsRes?.Data ?? new();
        SubjectOptions = subjects.Select(s => new SelectListItem
        {
            Value = s.Id.ToString(),
            Text = s.Code + " - " + s.Name,
            Selected = SubjectId.HasValue && s.Id == SubjectId.Value
        }).ToList();

        var categoriesRes = await _api.GetAsync<ApiResponse<List<LookupCategory>>>("api/lookup/categories");
        var categories = categoriesRes?.Data ?? new();
        CategoryOptions = categories.Select(c => new SelectListItem
        {
            Value = c.Id.ToString(),
            Text = c.Name,
            Selected = CategoryId.HasValue && c.Id == CategoryId.Value
        }).ToList();
    }

    // ==========================================
    // 6. GET STUDY HUB
    // ==========================================

    public async Task OnGetAsync()
    {
        await LoadFilterOptionsAsync();

        var url = "api/studyhub/materials";
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(Search))
            queryParams.Add($"search={Uri.EscapeDataString(Search.Trim())}");
        if (SubjectId.HasValue)
            queryParams.Add($"subjectId={SubjectId.Value}");
        if (CategoryId.HasValue)
            queryParams.Add($"categoryId={CategoryId.Value}");

        if (queryParams.Any())
            url += "?" + string.Join("&", queryParams);

        var result = await _api.GetAsync<ApiResponse<List<StudyMaterialDto>>>(url);
        Materials = result?.Data ?? new List<StudyMaterialDto>();

        // Chỉ Student mới xem tài liệu cá nhân
        if (!User.IsInRole("Student"))
            return;

        var myMatRes = await _api.GetAsync<ApiResponse<List<MyMaterialItem>>>("api/lookup/my-materials");
        MyMaterials = myMatRes?.Data ?? new List<MyMaterialItem>();
    }
}
