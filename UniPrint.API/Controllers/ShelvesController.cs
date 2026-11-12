using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Staff,Admin")]
public class ShelvesController : ControllerBase
{
    private readonly IShelfService _shelfService;

    public ShelvesController(IShelfService shelfService)
    {
        _shelfService = shelfService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _shelfService.GetAllShelvesAsync(ct);
        return Ok(result);
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(CancellationToken ct)
    {
        var result = await _shelfService.GetAvailableShelvesAsync(ct);
        return Ok(result);
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory(CancellationToken ct)
    {
        var result = await _shelfService.GetShelfInventoryAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _shelfService.GetShelfByIdAsync(id, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateShelfDto dto, CancellationToken ct)
    {
        var result = await _shelfService.CreateShelfAsync(dto, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateShelfDto dto, CancellationToken ct)
    {
        var result = await _shelfService.UpdateShelfAsync(id, dto, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _shelfService.DeleteShelfAsync(id, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Quét và tự động giải phóng/xóa các đơn hàng có mã QR nhận tài liệu đã hết hạn đang nằm trên kệ.
    /// </summary>
    [HttpPost("cleanup-expired")]
    public async Task<IActionResult> CleanupExpired([FromQuery] bool hardDelete = false, CancellationToken ct = default)
    {
        var result = await _shelfService.CleanupExpiredShelfOrdersAsync(hardDelete, ct);
        return Ok(result);
    }
}
