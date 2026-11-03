using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.Web.Pages.Admin;

/// <summary>Task 20: Admin cập nhật bảng giá in ấn hệ thống.</summary>
[Authorize(Roles = "Admin")]
public class PriceConfigModel : PageModel
{
    private readonly IPriceConfigService _priceConfigService;

    public PriceConfigModel(IPriceConfigService priceConfigService)
    {
        _priceConfigService = priceConfigService;
    }

    [BindProperty]
    public PriceConfigInput Input { get; set; } = new();

    public PriceConfigDto? Current { get; set; }
    public List<PriceConfigDto> History { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public List<string> Errors { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (Current != null)
        {
            Input = new PriceConfigInput
            {
                PricePerBWPage = Format(Current.PricePerBWPage),
                PricePerColorPage = Format(Current.PricePerColorPage),
                DoubleSidedDiscountPercent = Format(Current.DoubleSidedDiscountPercent),
                BindingPrice = Format(Current.BindingPrice),
                StaplePrice = Format(Current.StaplePrice)
            };
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
            return Challenge();

        // Ô number gửi dấu chấm thập phân; parse bằng InvariantCulture để không phụ thuộc culture vi-VN của máy chủ.
        var parsed = new[]
        {
            (Input.PricePerBWPage, "Giá in đen trắng"),
            (Input.PricePerColorPage, "Giá in màu"),
            (Input.DoubleSidedDiscountPercent, "Chiết khấu in 2 mặt"),
            (Input.BindingPrice, "Phí đóng gáy"),
            (Input.StaplePrice, "Phí bấm ghim")
        }.Select(f => (Value: TryParse(f.Item1), Label: f.Item2)).ToList();

        Errors = parsed.Where(p => p.Value == null).Select(p => $"{p.Label} không phải là số hợp lệ.").ToList();
        if (Errors.Count > 0)
        {
            await LoadAsync();
            return Page();
        }

        var result = await _priceConfigService.UpdatePriceConfigAsync(adminId, new UpdatePriceConfigDto
        {
            PricePerBWPage = parsed[0].Value!.Value,
            PricePerColorPage = parsed[1].Value!.Value,
            DoubleSidedDiscountPercent = parsed[2].Value!.Value,
            BindingPrice = parsed[3].Value!.Value,
            StaplePrice = parsed[4].Value!.Value
        });

        if (!result.Success)
        {
            Errors = result.Errors?.Count > 0 ? result.Errors : new List<string> { result.Message ?? "Cập nhật thất bại." };
            await LoadAsync();
            return Page();
        }

        SuccessMessage = result.Message;
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Current = (await _priceConfigService.GetActiveConfigAsync()).Data;
        History = (await _priceConfigService.GetHistoryAsync(10)).Data ?? new();
    }

    private static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static decimal? TryParse(string? raw)
    {
        var text = raw?.Trim().Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public class PriceConfigInput
    {
        [Display(Name = "Giá in đen trắng / trang")]
        public string? PricePerBWPage { get; set; }

        [Display(Name = "Giá in màu / trang")]
        public string? PricePerColorPage { get; set; }

        [Display(Name = "Chiết khấu in 2 mặt (%)")]
        public string? DoubleSidedDiscountPercent { get; set; }

        [Display(Name = "Phí đóng gáy / bản")]
        public string? BindingPrice { get; set; }

        [Display(Name = "Phí bấm ghim / bản")]
        public string? StaplePrice { get; set; }
    }
}
