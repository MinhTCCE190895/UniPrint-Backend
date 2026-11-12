using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;

namespace UniPrint.Business.Services;

public interface IPasswordResetOtpStore
{
    (string OtpCode, DateTime ExpiresAtUtc) IssueOtp(string email, TimeSpan? ttl = null);
    bool ValidateAndConsumeOtp(string email, string otpCode, out string? errorMessage);
    void Invalidate(string email);
}

public sealed class InMemoryPasswordResetOtpStore : IPasswordResetOtpStore
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);

    private sealed class OtpEntry
    {
        public string OtpCode { get; init; } = string.Empty;
        public DateTime ExpiresAtUtc { get; init; }
        public int FailedAttempts { get; set; }
    }

    private readonly Dictionary<string, OtpEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public (string OtpCode, DateTime ExpiresAtUtc) IssueOtp(string email, TimeSpan? ttl = null)
    {
        var normalizedEmail = NormalizeEmail(email);
        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
        var expiresAtUtc = DateTime.UtcNow.Add(ttl ?? DefaultTtl);

        lock (_sync)
        {
            _entries[normalizedEmail] = new OtpEntry
            {
                OtpCode = otpCode,
                ExpiresAtUtc = expiresAtUtc,
                FailedAttempts = 0
            };
        }

        return (otpCode, expiresAtUtc);
    }

    public bool ValidateAndConsumeOtp(string email, string otpCode, out string? errorMessage)
    {
        var normalizedEmail = NormalizeEmail(email);
        var trimmedOtp = (otpCode ?? string.Empty).Trim();

        lock (_sync)
        {
            if (!_entries.TryGetValue(normalizedEmail, out var entry))
            {
                errorMessage = "Yêu cầu đặt lại mật khẩu không tồn tại hoặc đã hết hạn. Vui lòng gửi lại mã OTP.";
                return false;
            }

            if (DateTime.UtcNow > entry.ExpiresAtUtc)
            {
                _entries.Remove(normalizedEmail);
                errorMessage = "Mã OTP đã hết hạn. Vui lòng gửi lại mã mới.";
                return false;
            }

            if (!string.Equals(entry.OtpCode, trimmedOtp, StringComparison.Ordinal))
            {
                entry.FailedAttempts++;
                if (entry.FailedAttempts >= MaxFailedAttempts)
                {
                    _entries.Remove(normalizedEmail);
                    errorMessage = $"Bạn đã nhập sai mã OTP quá {MaxFailedAttempts} lần. Vui lòng yêu cầu mã OTP mới.";
                }
                else
                {
                    errorMessage = $"Mã OTP không chính xác. Còn {MaxFailedAttempts - entry.FailedAttempts} lần thử.";
                }

                return false;
            }

            _entries.Remove(normalizedEmail);
            errorMessage = null;
            return true;
        }
    }

    public void Invalidate(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        lock (_sync)
        {
            _entries.Remove(normalizedEmail);
        }
    }

    private static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}

public interface IAuthService
{
    Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken ct = default);
    Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken ct = default);
    Task<ApiResponse<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default);
    Task<ApiResponse<UserProfileDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken ct = default);
    Task<ApiResponse<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request, CancellationToken ct = default);
    Task<ApiResponse<ForgotPasswordResponseDto>> ForgotPasswordAsync(ForgotPasswordRequestDto request, bool includeDemoOtp = true, CancellationToken ct = default);
    Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _config;
    private readonly IPasswordResetOtpStore _otpStore;

    public AuthService(
        IUnitOfWork unitOfWork,
        IConfiguration config,
        IPasswordResetOtpStore? otpStore = null)
    {
        _unitOfWork = unitOfWork;
        _config = config;
        _otpStore = otpStore ?? new InMemoryPasswordResetOtpStore();
    }

    public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken ct = default)
    {
        var userRepo = _unitOfWork.Repository<User>();
        var emailLower = request.Email.Trim().ToLower();

        if (await userRepo.ExistsAsync(u => u.Email.ToLower() == emailLower, ct))
        {
            return ApiResponse<AuthResponseDto>.Fail("Email đã được đăng ký trong hệ thống.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var newUser = new User
        {
            FullName = request.FullName.Trim(),
            Email = emailLower,
            PasswordHash = passwordHash,
            PhoneNumber = request.PhoneNumber,
            Role = UserRole.Student,
            IsActive = true
        };

        await userRepo.AddAsync(newUser, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var token = GenerateJwtToken(newUser);
        return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
        {
            AccessToken = token,
            UserId = newUser.Id,
            FullName = newUser.FullName,
            Email = newUser.Email,
            Role = newUser.Role
        }, "Đăng ký tài khoản thành công!");
    }

    public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        var userRepo = _unitOfWork.Repository<User>();
        var emailLower = request.Email.Trim().ToLower();

        var users = await userRepo.FindAsync(u => u.Email.ToLower() == emailLower, ct);
        var user = users.FirstOrDefault();

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return ApiResponse<AuthResponseDto>.Fail("Email hoặc mật khẩu không chính xác.");
        }

        if (!user.IsActive)
        {
            return ApiResponse<AuthResponseDto>.Fail("Tài khoản của bạn đã bị khóa.");
        }

        var token = GenerateJwtToken(user);
        return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
        {
            AccessToken = token,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role
        }, "Đăng nhập thành công!");
    }

    public async Task<ApiResponse<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId, ct);
        if (user == null)
        {
            return ApiResponse<UserProfileDto>.Fail("Không tìm thấy thông tin người dùng.");
        }

        return ApiResponse<UserProfileDto>.Ok(new UserProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            IsActive = user.IsActive
        });
    }

    public async Task<ApiResponse<UserProfileDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken ct = default)
    {
        var userRepo = _unitOfWork.Repository<User>();
        var user = await userRepo.GetByIdAsync(userId, ct);
        if (user == null)
        {
            return ApiResponse<UserProfileDto>.Fail("Không tìm thấy thông tin người dùng.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return ApiResponse<UserProfileDto>.Fail("Họ và tên không được để trống.");
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var phoneTrimmed = request.PhoneNumber.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(phoneTrimmed, @"^(0|\+84)[0-9]{9,10}$"))
            {
                return ApiResponse<UserProfileDto>.Fail("Số điện thoại không hợp lệ (cần 10 số, bắt đầu bằng 0 hoặc +84).");
            }
            user.PhoneNumber = phoneTrimmed;
        }
        else
        {
            user.PhoneNumber = null;
        }

        user.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
        {
            user.AvatarUrl = request.AvatarUrl.Trim();
        }

        userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<UserProfileDto>.Ok(new UserProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            IsActive = user.IsActive
        }, "Cập nhật thông tin cá nhân thành công!");
    }

    public async Task<ApiResponse<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request, CancellationToken ct = default)
    {
        var userRepo = _unitOfWork.Repository<User>();
        var user = await userRepo.GetByIdAsync(userId, ct);
        if (user == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy thông tin người dùng.");
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return ApiResponse<bool>.Fail("Mật khẩu hiện tại không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return ApiResponse<bool>.Fail("Mật khẩu mới không được để trống.");
        }

        if (request.NewPassword.Length < 6)
        {
            return ApiResponse<bool>.Fail("Mật khẩu mới phải có ít nhất 6 ký tự.");
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return ApiResponse<bool>.Fail("Xác nhận mật khẩu mới không khớp.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return ApiResponse<bool>.Fail("Mật khẩu hiện tại không chính xác.");
        }

        if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
        {
            return ApiResponse<bool>.Fail("Mật khẩu mới không được trùng với mật khẩu hiện tại.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Đổi mật khẩu thành công!");
    }

    public async Task<ApiResponse<ForgotPasswordResponseDto>> ForgotPasswordAsync(
        ForgotPasswordRequestDto request,
        bool includeDemoOtp = true,
        CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            return ApiResponse<ForgotPasswordResponseDto>.Fail("Vui lòng nhập địa chỉ email.");
        }

        var emailLower = request.Email.Trim().ToLower();
        if (!Regex.IsMatch(emailLower, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            return ApiResponse<ForgotPasswordResponseDto>.Fail("Định dạng email không hợp lệ.");
        }

        var userRepo = _unitOfWork.Repository<User>();
        var users = await userRepo.FindAsync(u => u.Email.ToLower() == emailLower, ct);
        var user = users.FirstOrDefault();

        if (user == null)
        {
            return ApiResponse<ForgotPasswordResponseDto>.Fail("Không tìm thấy tài khoản nào với email này.");
        }

        if (!user.IsActive)
        {
            return ApiResponse<ForgotPasswordResponseDto>.Fail("Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Quản trị viên để được hỗ trợ.");
        }

        var (otpCode, expiresAtUtc) = _otpStore.IssueOtp(user.Email);

        return ApiResponse<ForgotPasswordResponseDto>.Ok(
            new ForgotPasswordResponseDto
            {
                Email = user.Email,
                ExpiresAtUtc = expiresAtUtc,
                DemoOtpCode = includeDemoOtp ? otpCode : null
            },
            "Đã tạo mã OTP xác thực đặt lại mật khẩu (hiệu lực 10 phút).");
    }

    public async Task<ApiResponse<bool>> ResetPasswordAsync(
        ResetPasswordRequestDto request,
        CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            return ApiResponse<bool>.Fail("Email không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.OtpCode))
        {
            return ApiResponse<bool>.Fail("Vui lòng nhập mã OTP.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return ApiResponse<bool>.Fail("Mật khẩu mới không được để trống.");
        }

        if (request.NewPassword.Length < 6)
        {
            return ApiResponse<bool>.Fail("Mật khẩu mới phải có ít nhất 6 ký tự.");
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return ApiResponse<bool>.Fail("Xác nhận mật khẩu mới không khớp.");
        }

        var emailLower = request.Email.Trim().ToLower();
        var userRepo = _unitOfWork.Repository<User>();
        var users = await userRepo.FindAsync(u => u.Email.ToLower() == emailLower, ct);
        var user = users.FirstOrDefault();

        if (user == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy tài khoản nào với email này.");
        }

        if (!user.IsActive)
        {
            _otpStore.Invalidate(user.Email);
            return ApiResponse<bool>.Fail("Tài khoản của bạn đã bị khóa. Không thể đặt lại mật khẩu.");
        }

        if (!_otpStore.ValidateAndConsumeOtp(user.Email, request.OtpCode, out var otpError))
        {
            return ApiResponse<bool>.Fail(otpError ?? "Mã OTP không hợp lệ.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.");
    }

    private string GenerateJwtToken(User user)
    {
        var secretKey = _config["JwtSettings:Secret"] ?? "UniPrint_Super_Secret_Key_For_Course_Project_2026_FPTU_Must_Be_Long!";
        var issuer = _config["JwtSettings:Issuer"] ?? "UniPrint_API";
        var audience = _config["JwtSettings:Audience"] ?? "UniPrint_Client";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
