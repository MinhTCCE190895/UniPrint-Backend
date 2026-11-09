using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.Admin;

/// <summary>Task 20: Admin cập nhật bảng giá in ấn hệ thống qua REST API.</summary>
[Authorize(Roles = "Admin")]
public class PriceConfigModel : PageModel
{
    private readonly WebApiClient _api;

    public PriceConfigModel(WebApiClient api)
    {
        _api = api;
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
        var fields = new[]
        {
            (Name: "Giá in đen trắng", Value: TryParse(Input.PricePerBWPage)),
            (Name: "Giá in màu", Value: TryParse(Input.PricePerColorPage)),
            (Name: "Chiết khấu in 2 mặt", Value: TryParse(Input.DoubleSidedDiscountPercent)),
            (Name: "Phí đóng gáy", Value: TryParse(Input.BindingPrice)),
            (Name: "Phí bấm ghim", Value: TryParse(Input.StaplePrice))
        };

        foreach (var f in fields)
        {
            if (f.Value == null)
                Errors.Add($"{f.Name} không hợp lệ (phải là số).");
        }

        if (Errors.Count > 0)
        {
            await LoadAsync();
            return Page();
        }

        var result = await _api.PutAsync<ApiResponse<PriceConfigDto>>("api/price/config", new UpdatePriceConfigDto
        {
            PricePerBWPage = fields[0].Value!.Value,
            PricePerColorPage = fields[1].Value!.Value,
            DoubleSidedDiscountPercent = fields[2].Value!.Value,
            BindingPrice = fields[3].Value!.Value,
            StaplePrice = fields[4].Value!.Value
        });

        if (result == null || !result.Success)
        {
            Errors = result?.Errors?.Count > 0 ? result.Errors : new List<string> { result?.Message ?? "Cập nhật thất bại." };
            await LoadAsync();
            return Page();
        }

        SuccessMessage = result.Message;
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var currentRes = await _api.GetAsync<ApiResponse<PriceConfigDto>>("api/price/config");
        Current = currentRes?.Data;

        var histRes = await _api.GetAsync<ApiResponse<List<PriceConfigDto>>>("api/price/config/history?count=10");
        History = histRes?.Data ?? new();
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
