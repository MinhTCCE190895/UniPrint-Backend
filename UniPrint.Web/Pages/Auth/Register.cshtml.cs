using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.Auth;

public class RegisterModel : PageModel
{
    private readonly WebApiClient _api;

    public RegisterModel(WebApiClient api) => _api = api;

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên phải từ 2 đến 100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ email")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string? PhoneNumber { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có tối thiểu 6 ký tự")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var result = await _api.PostAsync<ApiResponse<AuthResponseDto>>("api/auth/register", new RegisterRequestDto
        {
            FullName = FullName,
            Email = Email,
            Password = Password,
            PhoneNumber = PhoneNumber
        });

        if (result == null || !result.Success || result.Data == null)
        {
            ErrorMessage = result?.Message ?? "Đăng ký không thành công. Vui lòng thử lại.";
            return Page();
        }

        // Lưu JWT token vào Session
        if (!string.IsNullOrEmpty(result.Data.AccessToken))
        {
            HttpContext.Session.SetString("jwt_token", result.Data.AccessToken);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Data.UserId.ToString()),
            new(ClaimTypes.Name, result.Data.FullName),
            new(ClaimTypes.Email, result.Data.Email),
            new(ClaimTypes.Role, result.Data.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToPage("/Index");
    }
}
