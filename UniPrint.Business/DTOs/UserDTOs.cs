using UniPrint.DataAccess.Enums;

namespace UniPrint.Business.DTOs;

/// <summary>
/// Task 5: DTO hiển thị thông tin người dùng trong danh sách quản lý của Admin.
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; }
    public string RoleName => Role switch
    {
        UserRole.Student => "Sinh viên",
        UserRole.Staff => "Nhân viên",
        UserRole.Admin => "Quản trị viên",
        _ => Role.ToString()
    };
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Task 5: Request DTO để Admin cập nhật quyền (Role) của người dùng.
/// </summary>
public class UpdateUserRoleDto
{
    public UserRole Role { get; set; }
}

/// <summary>
/// Task 5: Request DTO để Admin khóa hoặc mở khóa tài khoản người dùng.
/// </summary>
public class ToggleUserStatusDto
{
    public bool? IsActive { get; set; }
}

