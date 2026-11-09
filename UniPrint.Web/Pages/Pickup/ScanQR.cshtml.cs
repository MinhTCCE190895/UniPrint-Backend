using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.Pickup;

[Authorize(Roles = "Staff,Admin")]
public class ScanQRModel : PageModel
{
    private readonly WebApiClient _api;

    public ScanQRModel(WebApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public string QrToken { get; set; } = string.Empty;

    public string? Message { get; set; }
    public bool IsSuccess { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(QrToken))
        {
            IsSuccess = false;
            Message = "Vui lòng nhập hoặc quét mã QR.";
            return Page();
        }

        var result = await _api.PostAsync<ApiResponse<PrintOrderResponseDto>>($"api/printorders/scan-pickup-qr?qrToken={Uri.EscapeDataString(QrToken.Trim())}");

        IsSuccess = result != null && result.Success;
        Message = result?.Message ?? "Quét mã QR thất bại.";

        return Page();
    }
}
