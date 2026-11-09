using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.StudyHub;

public class DetailsModel : PageModel
{
    private readonly WebApiClient _api;
    private readonly IConfiguration _config;

    public DetailsModel(WebApiClient api, IConfiguration config)
    {
        _api = api;
        _config = config;
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

    private async Task<bool> LoadMaterialAsync(Guid id)
    {
        var matRes = await _api.GetAsync<ApiResponse<StudyMaterialDto>>($"api/lookup/study-material/{id}");
        if (matRes == null || !matRes.Success || matRes.Data == null)
            return false;

        Material = matRes.Data;

        var revRes = await _api.GetAsync<ApiResponse<List<ReviewItem>>>($"api/lookup/study-material-reviews/{id}");
        Reviews = revRes?.Data ?? new List<ReviewItem>();

        return true;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!await LoadMaterialAsync(id))
            return NotFound();

        if (User.IsInRole("Student"))
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userIdString, out var studentId))
            {
                var existing = Reviews.FirstOrDefault(r => r.StudentId == studentId);
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

    public async Task<IActionResult> OnPostReviewAsync(Guid id)
    {
        if (!User.IsInRole("Student"))
            return Forbid();
        if (!await LoadMaterialAsync(id))
            return NotFound();

        if (!ModelState.IsValid)
            return Page();

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var studentId))
            return Challenge();

        var reviewDto = new MaterialReviewDto
        {
            Rating = Rating,
            Comment = Comment?.Trim()
        };

        var result = await _api.PostAsync<ApiResponse<bool>>($"api/studyhub/materials/{id}/reviews", reviewDto);
        if (result == null || !result.Success)
        {
            ErrorMessage = result?.Message ?? "Không thể lưu đánh giá.";
            var existing = Reviews.FirstOrDefault(r => r.StudentId == studentId);
            HasReviewed = existing != null;
            return Page();
        }

        TempData["ReviewSuccess"] = result.Message ?? "Đã lưu đánh giá thành công!";
        return RedirectToPage("/StudyHub/Details", new { id });
    }

    public IActionResult OnGetDownload(Guid id)
    {
        var apiBaseUrl = _config["ApiSettings:BaseUrl"] ?? "http://localhost:5045";
        return Redirect($"{apiBaseUrl.TrimEnd('/')}/api/lookup/study-material/{id}/download");
    }
}
