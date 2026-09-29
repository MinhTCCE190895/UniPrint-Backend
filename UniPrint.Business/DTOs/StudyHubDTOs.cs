using UniPrint.DataAccess.Enums;

namespace UniPrint.Business.DTOs;

public class CreateMaterialDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid CategoryId { get; set; }
}

public class StudyMaterialDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string UploaderName { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string DocumentFileName { get; set; } = string.Empty;
    public MaterialStatus Status { get; set; }
    public int DownloadCount { get; set; }
    public int PrintCount { get; set; }
    public double AverageRating { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MaterialReviewDto
{
    public int Rating { get; set; } // 1 - 5
    public string? Comment { get; set; }
}

public class MaterialReportDto
{
    public string Reason { get; set; } = string.Empty;
}

public class ModerateMaterialDto
{
    public MaterialStatus NewStatus { get; set; } // Approved or Rejected
}
