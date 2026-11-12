using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.Auth;

public class ForgotPasswordModel : PageModel
{
    private readonly WebApiClient _api;

    public ForgotPasswordModel(WebApiClient api) => _api = api;

    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string OtpCode { get; set; } = string.Empty;
    [BindProperty] public string NewPassword { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;
    [BindProperty] public bool IsOtpSent { get; set; }
    [BindProperty] public string? DemoOtpCode { get; set; }
    [BindProperty] public DateTime? ExpiresAtUtc { get; set; }

    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }

    public void OnGet([FromQuery] string? email = null)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            Email = email.Trim();
        }
    }

    public async Task<IActionResult> OnPostSendOtpAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Vui lòng nhập email đã đăng ký.";
            return Page();
        }

        var result = await _api.PostAsync<ApiResponse<ForgotPasswordResponseDto>>(
            "api/auth/forgot-password",
            new ForgotPasswordRequestDto { Email = Email.Trim() });

        if (result == null || !result.Success)
        {
            ErrorMessage = result?.Message ?? "Không thể tạo mã OTP đặt lại mật khẩu.";
            return Page();
        }

        IsOtpSent = true;
        Email = result.Data?.Email ?? Email.Trim();
        DemoOtpCode = result.Data?.DemoOtpCode;
        ExpiresAtUtc = result.Data?.ExpiresAtUtc;
        OtpCode = string.Empty;
        InfoMessage = result.Message;

        return Page();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync()
    {
        IsOtpSent = true;

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Email không được để trống.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(OtpCode))
        {
            ErrorMessage = "Vui lòng nhập mã OTP 6 chữ số.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.";
            return Page();
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Xác nhận mật khẩu mới không khớp.";
            return Page();
        }

        var result = await _api.PostAsync<ApiResponse<bool>>(
            "api/auth/reset-password",
            new ResetPasswordRequestDto
            {
                Email = Email.Trim(),
                OtpCode = OtpCode.Trim(),
                NewPassword = NewPassword,
                ConfirmPassword = ConfirmPassword
            });

        if (result == null || !result.Success)
        {
            ErrorMessage = result?.Message ?? "Đặt lại mật khẩu thất bại.";
            return Page();
        }

        TempData["SuccessMessage"] = result.Message ?? "Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.";
        return RedirectToPage("/Auth/Login");
    }
}

