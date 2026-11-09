using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Enums;

namespace UniPrint.Web.Pages.Admin;

/// <summary>
/// Task 5: Admin quản lý người dùng - Xem danh sách, tìm kiếm, phân quyền Role, khóa/mở khóa tài khoản.
/// </summary>
[Authorize(Roles = "Admin")]
public class UsersModel : PageModel
{
    private readonly IUserService _userService;

    public UsersModel(IUserService userService)
    {
        _userService = userService;
    }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public UserRole? RoleFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? StatusFilter { get; set; }

    public List<UserDto> Users { get; set; } = new();

    public Guid CurrentAdminId { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    // Thống kê nhanh
    public int TotalUsers => Users.Count;
    public int StudentCount => Users.Count(u => u.Role == UserRole.Student);
    public int StaffCount => Users.Count(u => u.Role == UserRole.Staff);
    public int AdminCount => Users.Count(u => u.Role == UserRole.Admin);
    public int LockedCount => Users.Count(u => !u.IsActive);

    public async Task<IActionResult> OnGetAsync()
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
        {
            CurrentAdminId = adminId;
        }

        var result = await _userService.GetUsersAsync(SearchTerm, RoleFilter, StatusFilter);
        Users = result.Data ?? new List<UserDto>();

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateRoleAsync(Guid userId, UserRole newRole)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
        {
            return Challenge();
        }

        var result = await _userService.UpdateUserRoleAsync(adminId, userId, new UpdateUserRoleDto { Role = newRole });
        if (result.Success)
        {
            SuccessMessage = result.Message;
        }
        else
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage(new { SearchTerm, RoleFilter, StatusFilter });
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(Guid userId, bool? isActive = null)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
        {
            return Challenge();
        }

        var result = await _userService.ToggleUserStatusAsync(adminId, userId, new ToggleUserStatusDto { IsActive = isActive });
        if (result.Success)
        {
            SuccessMessage = result.Message;
        }
        else
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage(new { SearchTerm, RoleFilter, StatusFilter });
    }
}

