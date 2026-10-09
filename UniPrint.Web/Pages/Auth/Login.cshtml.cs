using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Enums;

namespace UniPrint.Web.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly IAuthService _authService;

    public LoginModel(IAuthService authService)
    {
        _authService = authService;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet([FromQuery] string? returnUrl = null)
    {
    }

    public async Task<IActionResult> OnPostAsync([FromQuery] string? returnUrl = null)
    {
        var result = await _authService.LoginAsync(new LoginRequestDto
        {
            Email = Email,
            Password = Password
        });

        if (!result.Success || result.Data == null)
        {
            ErrorMessage = result.Message ?? "Đăng nhập thất bại.";
            return Page();
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

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        // Điều hướng thông minh theo vai trò
        if (result.Data.Role == UserRole.Staff || result.Data.Role == UserRole.Admin)
        {
            return RedirectToPage("/Staff/Queue");
        }

        return RedirectToPage("/Student/CreateOrder");
    }
}
