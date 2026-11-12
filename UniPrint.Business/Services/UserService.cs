using Microsoft.EntityFrameworkCore;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Business.Services;

public interface IUserService
{
    Task<ApiResponse<List<UserDto>>> GetUsersAsync(string? searchTerm = null, UserRole? role = null, bool? isActive = null, CancellationToken ct = default);
    Task<ApiResponse<UserDto>> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<UserDto>> UpdateUserRoleAsync(Guid adminId, Guid targetUserId, UpdateUserRoleDto request, CancellationToken ct = default);
    Task<ApiResponse<UserDto>> ToggleUserStatusAsync(Guid adminId, Guid targetUserId, ToggleUserStatusDto? request = null, CancellationToken ct = default);
    Task<ApiResponse<UserDto>> CreateStaffAsync(CreateStaffRequestDto request, CancellationToken ct = default);
    Task<ApiResponse<UserDto>> ResetPasswordAsync(Guid adminId, Guid targetUserId, AdminResetPasswordDto request, CancellationToken ct = default);
}

/// <summary>
/// Task 5: Triển khai các nghiệp vụ quản lý người dùng cho Quản trị viên (Admin):
/// - Xem danh sách người dùng, tìm kiếm theo tên/email/phone, lọc theo Role và IsActive.
/// - Phân quyền người dùng (Role: Student / Staff / Admin).
/// - Khóa / Mở khóa tài khoản (IsActive).
/// </summary>
public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<List<UserDto>>> GetUsersAsync(
        string? searchTerm = null,
        UserRole? role = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        var query = _unitOfWork.Repository<User>().Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
        }

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                AvatarUrl = u.AvatarUrl,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<UserDto>>.Ok(users, $"Lấy danh sách {users.Count} người dùng thành công.");
    }

    public async Task<ApiResponse<UserDto>> GetUserByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(id, ct);
        if (user == null)
        {
            return ApiResponse<UserDto>.Fail("Không tìm thấy người dùng.");
        }

        return ApiResponse<UserDto>.Ok(MapToDto(user));
    }

    public async Task<ApiResponse<UserDto>> UpdateUserRoleAsync(
        Guid adminId,
        Guid targetUserId,
        UpdateUserRoleDto request,
        CancellationToken ct = default)
    {
        if (adminId == targetUserId)
        {
            return ApiResponse<UserDto>.Fail("Không thể tự thay đổi vai trò của tài khoản đang đăng nhập.");
        }

        if (request.Role == UserRole.Admin)
        {
            return ApiResponse<UserDto>.Fail("Không được phép phân quyền tài khoản lên Quản trị viên (Admin).");
        }

        if (!Enum.IsDefined(typeof(UserRole), request.Role))
        {
            return ApiResponse<UserDto>.Fail("Vai trò người dùng không hợp lệ.");
        }

        var userRepo = _unitOfWork.Repository<User>();
        var targetUser = await userRepo.GetByIdAsync(targetUserId, ct);
        if (targetUser == null)
        {
            return ApiResponse<UserDto>.Fail("Không tìm thấy người dùng.");
        }

        if (targetUser.Role == UserRole.Admin)
        {
            return ApiResponse<UserDto>.Fail("Không thể thay đổi vai trò của tài khoản Quản trị viên (Admin).");
        }

        targetUser.Role = request.Role;
        userRepo.Update(targetUser);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<UserDto>.Ok(MapToDto(targetUser), $"Cập nhật vai trò của {targetUser.FullName} thành {MapToDto(targetUser).RoleName} thành công.");
    }

    public async Task<ApiResponse<UserDto>> ToggleUserStatusAsync(
        Guid adminId,
        Guid targetUserId,
        ToggleUserStatusDto? request = null,
        CancellationToken ct = default)
    {
        if (adminId == targetUserId)
        {
            return ApiResponse<UserDto>.Fail("Không thể tự khóa tài khoản của chính mình.");
        }

        var userRepo = _unitOfWork.Repository<User>();
        var targetUser = await userRepo.GetByIdAsync(targetUserId, ct);
        if (targetUser == null)
        {
            return ApiResponse<UserDto>.Fail("Không tìm thấy người dùng.");
        }

        bool newStatus = request?.IsActive ?? !targetUser.IsActive;
        targetUser.IsActive = newStatus;
        userRepo.Update(targetUser);
        await _unitOfWork.SaveChangesAsync(ct);

        string statusText = newStatus
            ? $"Mở khóa tài khoản thành công ({targetUser.FullName})."
            : $"Khóa tài khoản thành công ({targetUser.FullName}).";
        return ApiResponse<UserDto>.Ok(MapToDto(targetUser), statusText);
    }

    public async Task<ApiResponse<UserDto>> CreateStaffAsync(CreateStaffRequestDto request, CancellationToken ct = default)
    {
        if (request == null)
        {
            return ApiResponse<UserDto>.Fail("Dữ liệu tạo nhân viên không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return ApiResponse<UserDto>.Fail("Họ và tên nhân viên không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return ApiResponse<UserDto>.Fail("Email nhân viên không được để trống.");
        }

        var emailLower = request.Email.Trim().ToLower();
        if (!System.Text.RegularExpressions.Regex.IsMatch(emailLower, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            return ApiResponse<UserDto>.Fail("Định dạng email không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return ApiResponse<UserDto>.Fail("Mật khẩu khởi tạo phải có ít nhất 6 ký tự.");
        }

        string? phoneTrimmed = null;
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            phoneTrimmed = request.PhoneNumber.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(phoneTrimmed, @"^(0|\+84)[0-9]{9,10}$"))
            {
                return ApiResponse<UserDto>.Fail("Số điện thoại không hợp lệ (cần 10 số, bắt đầu bằng 0 hoặc +84).");
            }
        }

        var userRepo = _unitOfWork.Repository<User>();
        if (await userRepo.ExistsAsync(u => u.Email.ToLower() == emailLower, ct))
        {
            return ApiResponse<UserDto>.Fail("Email đã được đăng ký trong hệ thống.");
        }

        var staff = new User
        {
            FullName = request.FullName.Trim(),
            Email = emailLower,
            PhoneNumber = phoneTrimmed,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Staff,
            IsActive = true
        };

        await userRepo.AddAsync(staff, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<UserDto>.Ok(MapToDto(staff), $"Tạo tài khoản nhân viên {staff.FullName} ({staff.Email}) thành công.");
    }

    public async Task<ApiResponse<UserDto>> ResetPasswordAsync(
        Guid adminId,
        Guid targetUserId,
        AdminResetPasswordDto request,
        CancellationToken ct = default)
    {
        if (adminId == targetUserId)
        {
            return ApiResponse<UserDto>.Fail("Không thể tự đặt lại mật khẩu tại đây. Vui lòng dùng trang Hồ sơ cá nhân.");
        }

        if (request == null || string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return ApiResponse<UserDto>.Fail("Mật khẩu mới phải có ít nhất 6 ký tự.");
        }

        var userRepo = _unitOfWork.Repository<User>();
        var targetUser = await userRepo.GetByIdAsync(targetUserId, ct);
        if (targetUser == null)
        {
            return ApiResponse<UserDto>.Fail("Không tìm thấy người dùng.");
        }

        if (targetUser.Role == UserRole.Admin)
        {
            return ApiResponse<UserDto>.Fail("Không được phép đặt lại mật khẩu của tài khoản Quản trị viên (Admin).");
        }

        targetUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        userRepo.Update(targetUser);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<UserDto>.Ok(MapToDto(targetUser), $"Đặt lại mật khẩu cho {targetUser.FullName} ({targetUser.Email}) thành công.");
    }

    private static UserDto MapToDto(User u) => new()
    {
        Id = u.Id,
        FullName = u.FullName,
        Email = u.Email,
        PhoneNumber = u.PhoneNumber,
        AvatarUrl = u.AvatarUrl,
        Role = u.Role,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt
    };
}

