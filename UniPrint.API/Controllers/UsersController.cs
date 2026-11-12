using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Enums;

namespace UniPrint.API.Controllers;

/// <summary>
/// Task 5: Controller quản lý người dùng dành cho Quản trị viên (Admin).
/// Hỗ trợ xem danh sách, lọc, phân quyền Role, và khóa/mở khóa tài khoản.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Task 5: Admin xem danh sách người dùng trong hệ thống với các bộ lọc:
    /// tìm kiếm theo tên, email, SĐT; lọc theo vai trò (Role); lọc theo trạng thái (IsActive).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? searchTerm = null,
        [FromQuery] UserRole? role = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _userService.GetUsersAsync(searchTerm, role, isActive, ct);
        return Ok(result);
    }

    /// <summary>
    /// Task 5: Xem chi tiết thông tin người dùng theo Id.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken ct = default)
    {
        var result = await _userService.GetUserByIdAsync(id, ct);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// Task 5: Admin phân quyền (Role) cho người dùng (Student = 1, Staff = 2, Admin = 3).
    /// Không cho phép Admin tự thay đổi quyền hạn của chính mình.
    /// </summary>
    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> UpdateUserRole(
        Guid id,
        [FromBody] UpdateUserRoleDto request,
        CancellationToken ct = default)
    {
        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(adminIdStr, out var adminId)) return Unauthorized();

        var result = await _userService.UpdateUserRoleAsync(adminId, id, request, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// Task 5: Admin khóa hoặc mở khóa tài khoản người dùng (IsActive).
    /// Khi tài khoản bị khóa, người dùng sẽ không thể đăng nhập vào hệ thống.
    /// Không cho phép Admin tự khóa tài khoản của chính mình.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> ToggleUserStatus(
        Guid id,
        [FromBody] ToggleUserStatusDto? request = null,
        CancellationToken ct = default)
    {
        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(adminIdStr, out var adminId)) return Unauthorized();

        var result = await _userService.ToggleUserStatusAsync(adminId, id, request, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>
    /// BR01: Admin tạo trực tiếp tài khoản Nhân viên (Staff).
    /// </summary>
    [HttpPost("staff")]
    public async Task<IActionResult> CreateStaff(
        [FromBody] CreateStaffRequestDto request,
        CancellationToken ct = default)
    {
        var result = await _userService.CreateStaffAsync(request, ct);
        if (!result.Success) return BadRequest(result);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Admin đặt lại mật khẩu cho tài khoản người dùng (không áp dụng cho chính mình hoặc Admin khác).
    /// </summary>
    [HttpPut("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] AdminResetPasswordDto request,
        CancellationToken ct = default)
    {
        var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(adminIdStr, out var adminId)) return Unauthorized();

        var result = await _userService.ResetPasswordAsync(adminId, id, request, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}

