using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly WebApiClient _api;

    public LoginModel(WebApiClient api) => _api = api;

    [BindProperty] public string Email    { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet([FromQuery] string? returnUrl = null) { }

    public async Task<IActionResult> OnPostAsync([FromQuery] string? returnUrl = null)
    {
        var result = await _api.PostAsync<ApiResponse<AuthResponseDto>>("api/auth/login", new LoginRequestDto
        {
            Email = Email,
            Password = Password
        });

        if (result == null || !result.Success || result.Data == null)
        {
            ErrorMessage = result?.Message ?? "Đăng nhập thất bại.";
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

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToPage("/Index");
    }
}
