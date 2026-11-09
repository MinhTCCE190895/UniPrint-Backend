using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.DataAccess.Enums;
using UniPrint.Web.Services;

namespace UniPrint.Web.Pages.Student;

[Authorize(Roles = "Student")]
public class CreateOrderModel : PageModel
{
    private readonly WebApiClient _api;

    public CreateOrderModel(WebApiClient api)
    {
        _api = api;
    }

    public List<DocumentResponseDto> MyDocuments { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid? DocumentId { get; set; }

    public string? SelectedDocumentName { get; set; }

    [BindProperty]
    public int PageCount { get; set; } = 1;

    [BindProperty]
    public int NumberOfCopies { get; set; } = 1;

    [BindProperty]
    public bool IsColor { get; set; }

    [BindProperty]
    public bool IsDoubleSided { get; set; } = true;

    [BindProperty]
    public bool HasBinding { get; set; }

    [BindProperty]
    public bool HasStaple { get; set; } = true;

    [BindProperty]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [BindProperty]
    public string? Notes { get; set; }

    public PriceCalculationResultDto? EstimatedPrice { get; set; }

    public PrintOrderResponseDto? CreatedOrder { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadDocumentsAsync();

        if (!DocumentId.HasValue && MyDocuments.Any())
        {
            DocumentId = MyDocuments.First().Id;
        }

        if (DocumentId.HasValue)
        {
            var doc = MyDocuments.FirstOrDefault(d => d.Id == DocumentId.Value);
            if (doc != null)
            {
                SelectedDocumentName = doc.FileName;
                PageCount = doc.PageCount;
                await CalculatePriceAsync();
            }
            else if (MyDocuments.Any())
            {
                var fallbackDoc = MyDocuments.First();
                DocumentId = fallbackDoc.Id;
                SelectedDocumentName = fallbackDoc.FileName;
                PageCount = fallbackDoc.PageCount;
                await CalculatePriceAsync();
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnGetCalculatePriceAsync(Guid documentId, int copies, bool isColor, bool isDoubleSided, bool hasBinding, bool hasStaple)
    {
        var docRes = await _api.GetAsync<ApiResponse<DocumentResponseDto>>($"api/documents/{documentId}");
        int pageCount = docRes != null && docRes.Success && docRes.Data != null ? docRes.Data.PageCount : 1;

        var priceResult = await _api.PostAsync<ApiResponse<PriceCalculationResultDto>>("api/price/calculate", new CalculatePriceRequestDto
        {
            PageCount = pageCount,
            NumberOfCopies = copies > 0 ? copies : 1,
            IsColor = isColor,
            IsDoubleSided = isDoubleSided,
            HasBinding = hasBinding,
            HasStaple = hasStaple
        });

        return new JsonResult(priceResult);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadDocumentsAsync();

        if (!DocumentId.HasValue || DocumentId.Value == Guid.Empty)
        {
            ErrorMessage = "Vui lòng chọn tài liệu cần in.";
            return Page();
        }

        var doc = MyDocuments.FirstOrDefault(d => d.Id == DocumentId.Value);
        if (doc != null)
        {
            SelectedDocumentName = doc.FileName;
            PageCount = doc.PageCount;
        }

        if (NumberOfCopies <= 0)
        {
            ErrorMessage = "Số bản in phải lớn hơn hoặc bằng 1.";
            await CalculatePriceAsync();
            return Page();
        }

        var request = new CreatePrintOrderDto
        {
            DocumentId = DocumentId.Value,
            NumberOfCopies = NumberOfCopies,
            IsColor = IsColor,
            IsDoubleSided = IsDoubleSided,
            HasBinding = HasBinding,
            HasStaple = HasStaple,
            PaymentMethod = PaymentMethod,
            Notes = Notes
        };

        var result = await _api.PostAsync<ApiResponse<PrintOrderResponseDto>>("api/printorders", request);
        if (result == null || !result.Success || result.Data == null)
        {
            ErrorMessage = result?.Message ?? "Tạo đơn đặt in thất bại.";
            await CalculatePriceAsync();
            return Page();
        }

        CreatedOrder = result.Data;
        StatusMessage = $"Đặt in thành công! Mã đơn của bạn là: {CreatedOrder.OrderCode}";
        return Page();
    }

    private async Task LoadDocumentsAsync()
    {
        var docsResult = await _api.GetAsync<ApiResponse<List<DocumentResponseDto>>>("api/documents/my-documents");
        if (docsResult != null && docsResult.Success && docsResult.Data != null)
        {
            MyDocuments = docsResult.Data;
        }
    }

    private async Task CalculatePriceAsync()
    {
        if (PageCount > 0)
        {
            var priceResult = await _api.PostAsync<ApiResponse<PriceCalculationResultDto>>("api/price/calculate", new CalculatePriceRequestDto
            {
                PageCount = PageCount,
                NumberOfCopies = NumberOfCopies > 0 ? NumberOfCopies : 1,
                IsColor = IsColor,
                IsDoubleSided = IsDoubleSided,
                HasBinding = HasBinding,
                HasStaple = HasStaple
            });

            if (priceResult != null && priceResult.Success)
            {
                EstimatedPrice = priceResult.Data;
            }
        }
    }
}
