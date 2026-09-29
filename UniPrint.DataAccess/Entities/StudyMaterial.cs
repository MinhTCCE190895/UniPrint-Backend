using UniPrint.DataAccess.Enums;

namespace UniPrint.DataAccess.Entities;

public class StudyMaterial : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Guid DocumentId { get; set; }
    public virtual Document Document { get; set; } = null!;

    public Guid SubjectId { get; set; }
    public virtual Subject Subject { get; set; } = null!;

    public Guid CategoryId { get; set; }
    public virtual Category Category { get; set; } = null!;

    public Guid UploadedByStudentId { get; set; }
    public virtual User UploadedByStudent { get; set; } = null!;

    public MaterialStatus Status { get; set; } = MaterialStatus.Pending;
    public int DownloadCount { get; set; } = 0;
    public int PrintCount { get; set; } = 0;
    public double AverageRating { get; set; } = 0.0;

    public virtual ICollection<MaterialReview> Reviews { get; set; } = new List<MaterialReview>();
    public virtual ICollection<MaterialReport> Reports { get; set; } = new List<MaterialReport>();
    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}
