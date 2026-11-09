
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Enums;

namespace UniPrint.Web.Pages.Admin;

[Authorize(Roles = "Admin")]
public class StudyMaterialsModel : PageModel
{
    private readonly IStudyHubService _studyHubService;

    public StudyMaterialsModel(
        IStudyHubService studyHubService)
    {
        _studyHubService = studyHubService;
    }

    public List<StudyMaterialDto> PendingMaterials
    { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var result = await _studyHubService
                .GetPendingMaterialsForAdminAsync();

            if (!result.Success)
            {
                ErrorMessage = result.Message
                    ?? "Không thể tải danh sách tài liệu.";
                return;
            }

            PendingMaterials = result.Data?.ToList()
                ?? new List<StudyMaterialDto>();
        }
        catch (Exception)
        {
            ErrorMessage =
                "Có lỗi khi tải tài liệu chờ duyệt.";
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(
        Guid id)
    {
        return await ModerateAsync(
            id, MaterialStatus.Approved);
    }

    public async Task<IActionResult> OnPostRejectAsync(
        Guid id)
    {
        return await ModerateAsync(
            id, MaterialStatus.Rejected);
    }

    private async Task<IActionResult> ModerateAsync(
        Guid id,
        MaterialStatus status)
    {
        if (id == Guid.Empty)
        {
            ErrorMessage = "ID tài liệu không hợp lệ.";
            return RedirectToPage();
        }

        try
        {
            var result = await _studyHubService
                .ModerateMaterialAsync(id, status);

            if (!result.Success)
            {
                ErrorMessage = result.Message
                    ?? "Không thể cập nhật trạng thái.";
            }
            else
            {
                SuccessMessage =
                    status == MaterialStatus.Approved
                    ? "Duyệt tài liệu thành công!"
                    : "Từ chối tài liệu thành công!";
            }
        }
        catch (Exception)
        {
            ErrorMessage =
                "Có lỗi xảy ra khi duyệt tài liệu.";
        }

        return RedirectToPage();
    }
}
