using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.Web.Pages.Auth;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly IAuthService _authService;

    public ProfileModel(IAuthService authService)
    {
        _authService = authService;
    }

    public UserProfileDto? UserProfile { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Họ và tên không được để trống.")]
    [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [BindProperty]
    public string? PhoneNumber { get; set; }

    [BindProperty]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? PasswordSuccessMessage { get; set; }

    [TempData]
    public string? PasswordErrorMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string ActiveTab { get; set; } = "info";

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return RedirectToPage("/Auth/Login");
        }

        var result = await _authService.GetProfileAsync(userId.Value);
        if (!result.Success || result.Data == null)
        {
            ErrorMessage = result.Message ?? "Không tìm thấy thông tin người dùng.";
            return RedirectToPage("/Index");
        }

        UserProfile = result.Data;
        FullName = result.Data.FullName;
        PhoneNumber = result.Data.PhoneNumber;

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateProfileAsync()
    {
        ActiveTab = "info";
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return RedirectToPage("/Auth/Login");
        }

        if (string.IsNullOrWhiteSpace(FullName))
        {
            ErrorMessage = "Họ và tên không được để trống.";
            await LoadUserProfileAsync(userId.Value);
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(PhoneNumber))
        {
            var phoneTrimmed = PhoneNumber.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(phoneTrimmed, @"^(0|\+84)[0-9]{9,10}$"))
            {
                ErrorMessage = "Số điện thoại không hợp lệ (cần 10 số, bắt đầu bằng 0 hoặc +84).";
                await LoadUserProfileAsync(userId.Value);
                return Page();
            }
        }

        var result = await _authService.UpdateProfileAsync(userId.Value, new UpdateProfileRequestDto
        {
            FullName = FullName,
            PhoneNumber = PhoneNumber
        });

        if (!result.Success || result.Data == null)
        {
            ErrorMessage = result.Message ?? "Cập nhật thông tin thất bại.";
            await LoadUserProfileAsync(userId.Value);
            return Page();
        }

        // Cập nhật lại Claims trong Cookie để header hiển thị tên mới ngay lập tức
        if (User.Identity is ClaimsIdentity identity)
        {
            var existingNameClaim = identity.FindFirst(ClaimTypes.Name);
            if (existingNameClaim != null)
            {
                identity.RemoveClaim(existingNameClaim);
            }
            identity.AddClaim(new Claim(ClaimTypes.Name, result.Data.FullName));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }

        SuccessMessage = result.Message ?? "Cập nhật thông tin cá nhân thành công!";
        return RedirectToPage("/Auth/Profile", new { activeTab = "info" });
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        ActiveTab = "password";
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return RedirectToPage("/Auth/Login");
        }

        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            PasswordErrorMessage = "Mật khẩu hiện tại không được để trống.";
            await LoadUserProfileAsync(userId.Value);
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            PasswordErrorMessage = "Mật khẩu mới không được để trống.";
            await LoadUserProfileAsync(userId.Value);
            return Page();
        }

        if (NewPassword.Length < 6)
        {
            PasswordErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.";
            await LoadUserProfileAsync(userId.Value);
            return Page();
        }

        if (NewPassword != ConfirmPassword)
        {
            PasswordErrorMessage = "Xác nhận mật khẩu mới không khớp.";
            await LoadUserProfileAsync(userId.Value);
            return Page();
        }

        var result = await _authService.ChangePasswordAsync(userId.Value, new ChangePasswordRequestDto
        {
            CurrentPassword = CurrentPassword,
            NewPassword = NewPassword,
            ConfirmPassword = ConfirmPassword
        });

        if (!result.Success)
        {
            PasswordErrorMessage = result.Message ?? "Đổi mật khẩu thất bại.";
            await LoadUserProfileAsync(userId.Value);
            return Page();
        }

        PasswordSuccessMessage = result.Message ?? "Đổi mật khẩu thành công!";
        return RedirectToPage("/Auth/Profile", new { activeTab = "password" });
    }

    private async Task LoadUserProfileAsync(Guid userId)
    {
        var result = await _authService.GetProfileAsync(userId);
        if (result.Success && result.Data != null)
        {
            UserProfile = result.Data;
        }
    }

    private Guid? GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(idClaim, out var userId))
        {
            return userId;
        }
        return null;
    }
}
