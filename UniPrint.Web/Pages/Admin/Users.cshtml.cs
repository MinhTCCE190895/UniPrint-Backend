using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Enums;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.Admin;

/// <summary>
/// Task 5: Admin quản lý người dùng - Xem danh sách, tìm kiếm, phân quyền Role, khóa/mở khóa tài khoản qua REST API.
/// </summary>
[Authorize(Roles = "Admin")]
public class UsersModel : PageModel
{
    private readonly WebApiClient _api;

    public UsersModel(WebApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public UserRole? RoleFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? StatusFilter { get; set; }

    [BindProperty]
    public CreateStaffRequestDto CreateStaffInput { get; set; } = new();

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

        var url = "api/users";
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(SearchTerm))
            queryParams.Add($"searchTerm={Uri.EscapeDataString(SearchTerm)}");
        if (RoleFilter.HasValue)
            queryParams.Add($"role={RoleFilter.Value}");
        if (StatusFilter.HasValue)
            queryParams.Add($"isActive={StatusFilter.Value.ToString().ToLower()}");

        if (queryParams.Any())
            url += "?" + string.Join("&", queryParams);

        var result = await _api.GetAsync<ApiResponse<List<UserDto>>>(url);
        Users = result?.Data ?? new List<UserDto>();

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateRoleAsync(Guid userId, UserRole newRole)
    {
        var result = await _api.PutAsync<ApiResponse<UserDto>>($"api/users/{userId}/role", new UpdateUserRoleDto
        {
            Role = newRole
        });

        if (result != null && result.Success)
        {
            SuccessMessage = string.IsNullOrWhiteSpace(result.Message)
                ? "Cập nhật vai trò thành công!"
                : result.Message;
        }
        else
        {
            ErrorMessage = result?.Message ?? "Cập nhật quyền thất bại.";
        }

        return RedirectToPage(new { SearchTerm, RoleFilter, StatusFilter });
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(Guid userId, bool? isActive = null)
    {
        var result = await _api.PutAsync<ApiResponse<UserDto>>($"api/users/{userId}/status", new ToggleUserStatusDto
        {
            IsActive = isActive
        });

        if (result != null && result.Success)
        {
            SuccessMessage = string.IsNullOrWhiteSpace(result.Message)
                ? "Cập nhật trạng thái tài khoản thành công!"
                : result.Message;
        }
        else
        {
            ErrorMessage = result?.Message ?? "Cập nhật trạng thái thất bại.";
        }

        return RedirectToPage(new { SearchTerm, RoleFilter, StatusFilter });
    }

    public async Task<IActionResult> OnPostCreateStaffAsync()
    {
        if (string.IsNullOrWhiteSpace(CreateStaffInput.FullName) ||
            string.IsNullOrWhiteSpace(CreateStaffInput.Email) ||
            string.IsNullOrWhiteSpace(CreateStaffInput.Password))
        {
            ErrorMessage = "Vui lòng nhập đầy đủ họ tên, email và mật khẩu cho nhân viên.";
            return RedirectToPage(new { SearchTerm, RoleFilter, StatusFilter });
        }

        var result = await _api.PostAsync<ApiResponse<UserDto>>("api/users/staff", CreateStaffInput);

        if (result != null && result.Success)
        {
            SuccessMessage = string.IsNullOrWhiteSpace(result.Message)
                ? "Tạo tài khoản nhân viên (Staff) thành công!"
                : result.Message;
        }
        else
        {
            ErrorMessage = result?.Message ?? "Tạo tài khoản nhân viên thất bại.";
        }

        return RedirectToPage(new { SearchTerm, RoleFilter, StatusFilter });
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(Guid userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.";
            return RedirectToPage(new { SearchTerm, RoleFilter, StatusFilter });
        }

        var result = await _api.PutAsync<ApiResponse<UserDto>>(
            $"api/users/{userId}/reset-password",
            new AdminResetPasswordDto { NewPassword = newPassword });

        if (result != null && result.Success)
        {
            SuccessMessage = string.IsNullOrWhiteSpace(result.Message)
                ? "Đặt lại mật khẩu thành công!"
                : result.Message;
        }
        else
        {
            ErrorMessage = result?.Message ?? "Đặt lại mật khẩu thất bại.";
        }

        return RedirectToPage(new { SearchTerm, RoleFilter, StatusFilter });
    }
}
