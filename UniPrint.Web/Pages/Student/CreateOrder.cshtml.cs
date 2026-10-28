using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Enums;

namespace UniPrint.Web.Pages.Student;

[Authorize(Roles = "Student")]
public class CreateOrderModel : PageModel
{
    private readonly IPriceCalculatorService _priceCalculator;
    private readonly IPrintOrderService _orderService;
    private readonly IDocumentService _documentService;

    public CreateOrderModel(
        IPriceCalculatorService priceCalculator,
        IPrintOrderService orderService,
        IDocumentService documentService)
    {
        _priceCalculator = priceCalculator;
        _orderService = orderService;
        _documentService = documentService;
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
        var studentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(studentIdStr, out var studentId))
        {
            return RedirectToPage("/Auth/Login");
        }

        await LoadDocumentsAsync(studentId);

        if (DocumentId.HasValue)
        {
            var doc = MyDocuments.FirstOrDefault(d => d.Id == DocumentId.Value);
            if (doc != null)
            {
                SelectedDocumentName = doc.FileName;
                PageCount = doc.PageCount;
                await CalculatePriceAsync();
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnGetCalculatePriceAsync(Guid documentId, int copies, bool isColor, bool isDoubleSided, bool hasBinding, bool hasStaple)
    {
        var studentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(studentIdStr, out var studentId))
        {
            return Unauthorized();
        }

        var docResult = await _documentService.GetDocumentByIdAsync(documentId, studentId);
        int pageCount = docResult.Success && docResult.Data != null ? docResult.Data.PageCount : 1;

        var priceResult = await _priceCalculator.CalculatePriceAsync(new CalculatePriceRequestDto
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
        var studentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(studentIdStr, out var studentId))
        {
            return RedirectToPage("/Auth/Login");
        }

        await LoadDocumentsAsync(studentId);

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

        var result = await _orderService.CreateOrderAsync(studentId, request);
        if (!result.Success || result.Data == null)
        {
            ErrorMessage = result.Message ?? "Tạo đơn đặt in thất bại.";
            await CalculatePriceAsync();
            return Page();
        }

        CreatedOrder = result.Data;
        StatusMessage = $"Đặt in thành công! Mã đơn của bạn là: {CreatedOrder.OrderCode}";
        return Page();
    }

    private async Task LoadDocumentsAsync(Guid studentId)
    {
        var docsResult = await _documentService.GetMyDocumentsAsync(studentId);
        if (docsResult.Success && docsResult.Data != null)
        {
            MyDocuments = docsResult.Data;
        }
    }

    private async Task CalculatePriceAsync()
    {
        if (PageCount > 0)
        {
            var priceResult = await _priceCalculator.CalculatePriceAsync(new CalculatePriceRequestDto
            {
                PageCount = PageCount,
                NumberOfCopies = NumberOfCopies > 0 ? NumberOfCopies : 1,
                IsColor = IsColor,
                IsDoubleSided = IsDoubleSided,
                HasBinding = HasBinding,
                HasStaple = HasStaple
            });

            if (priceResult.Success)
            {
                EstimatedPrice = priceResult.Data;
            }
        }
    }
}
