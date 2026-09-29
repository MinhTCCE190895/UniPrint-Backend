using Microsoft.AspNetCore.Mvc.RazorPages;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;

namespace UniPrint.Web.Pages.StudyHub;

public class IndexModel : PageModel
{
    private readonly IStudyHubService _studyHubService;

    public IndexModel(IStudyHubService studyHubService)
    {
        _studyHubService = studyHubService;
    }

    public List<StudyMaterialDto> Materials { get; set; } = new();

    public async Task OnGetAsync()
    {
        var result = await _studyHubService.GetApprovedMaterialsAsync(null, null, null);
        Materials = result.Data ?? new List<StudyMaterialDto>();
    }
}
