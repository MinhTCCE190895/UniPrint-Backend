using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    /// <summary>
    /// Task 6: Tải tài liệu PDF/DOCX lên hệ thống, tự động đọc số trang và kích thước file
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<DocumentResponseDto>.Fail("Vui lòng chọn file tài liệu hợp lệ để tải lên."));
        }

        var userId = GetCurrentUserId();
        using var stream = file.OpenReadStream();
        var result = await _documentService.UploadDocumentStreamAsync(userId, stream, file.FileName, file.Length);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Tải tài liệu theo metadata DTO (hỗ trợ tương thích ngược cho API clients)
    /// </summary>
    [HttpPost("upload-metadata")]
    public async Task<IActionResult> UploadMetadata([FromBody] DocumentUploadDto uploadDto)
    {
        var userId = GetCurrentUserId();
        var result = await _documentService.UploadDocumentAsync(userId, uploadDto);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách tài liệu của sinh viên hiện tại
    /// </summary>
    [HttpGet("my-documents")]
    public async Task<IActionResult> GetMyDocuments()
    {
        var userId = GetCurrentUserId();
        var result = await _documentService.GetMyDocumentsAsync(userId);
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một tài liệu theo Id
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDocumentById(Guid id)
    {
        var userId = GetCurrentUserId();
        var result = await _documentService.GetDocumentByIdAsync(id, userId);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Task 7: Xóa tài liệu cá nhân đã tải lên (chỉ chủ sở hữu tài liệu mới được xóa)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        var userId = GetCurrentUserId();
        var result = await _documentService.DeleteDocumentAsync(id, userId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(userIdStr!);
    }
}
