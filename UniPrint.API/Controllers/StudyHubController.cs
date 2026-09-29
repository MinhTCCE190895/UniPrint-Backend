using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudyHubController : ControllerBase
{
    private readonly IStudyHubService _studyHubService;

    public StudyHubController(IStudyHubService studyHubService)
    {
        _studyHubService = studyHubService;
    }

    [HttpGet("materials")]
    public async Task<IActionResult> GetApprovedMaterials(
        [FromQuery] string? search,
        [FromQuery] Guid? subjectId,
        [FromQuery] Guid? categoryId)
    {
        var result = await _studyHubService.GetApprovedMaterialsAsync(search, subjectId, categoryId);
        return Ok(result);
    }

    [Authorize(Roles = "Student")]
    [HttpPost("materials")]
    public async Task<IActionResult> UploadMaterial([FromBody] CreateMaterialDto request)
    {
        var studentId = GetCurrentUserId();
        var result = await _studyHubService.UploadMaterialAsync(studentId, request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin/pending-materials")]
    public async Task<IActionResult> GetPendingMaterials()
    {
        var result = await _studyHubService.GetPendingMaterialsForAdminAsync();
        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("admin/materials/{id}/moderate")]
    public async Task<IActionResult> ModerateMaterial(Guid id, [FromBody] ModerateMaterialDto dto)
    {
        var result = await _studyHubService.ModerateMaterialAsync(id, dto.NewStatus);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [Authorize(Roles = "Student")]
    [HttpPost("materials/{id}/reviews")]
    public async Task<IActionResult> AddReview(Guid id, [FromBody] MaterialReviewDto reviewDto)
    {
        var studentId = GetCurrentUserId();
        var result = await _studyHubService.AddReviewAsync(id, studentId, reviewDto);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [Authorize(Roles = "Student")]
    [HttpPost("materials/{id}/reports")]
    public async Task<IActionResult> ReportMaterial(Guid id, [FromBody] MaterialReportDto reportDto)
    {
        var studentId = GetCurrentUserId();
        var result = await _studyHubService.ReportMaterialAsync(id, studentId, reportDto);
        return Ok(result);
    }

    [HttpPost("materials/{id}/download-count")]
    public async Task<IActionResult> IncrementDownloadCount(Guid id)
    {
        var result = await _studyHubService.IncrementDownloadCountAsync(id);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdStr!);
    }
}
