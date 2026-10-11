using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.Web.Pages.Student;

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
    public int PageCount { get; set; } = 10;

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

    public async Task<IActionResult> OnGetAsync()
    {
        var studentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(studentIdStr, out var studentId))
        {
            return RedirectToPage("/Auth/Login");
        }

        var docsResult = await _documentService.GetMyDocumentsAsync(studentId);
        if (docsResult.Success && docsResult.Data != null)
        {
            MyDocuments = docsResult.Data;
        }

        if (DocumentId.HasValue)
        {
            var doc = MyDocuments.FirstOrDefault(d => d.Id == DocumentId.Value);
            if (doc != null)
            {
                SelectedDocumentName = doc.FileName;
                PageCount = doc.PageCount;
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var studentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(studentIdStr, out var studentId))
        {
            return RedirectToPage("/Auth/Login");
        }

        // Logic demo tạo đơn
        await Task.CompletedTask;
        return RedirectToPage("/Student/Documents");
    }
}
