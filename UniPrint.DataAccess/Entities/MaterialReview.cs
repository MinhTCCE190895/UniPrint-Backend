namespace UniPrint.DataAccess.Entities;

public class MaterialReview : BaseEntity
{
    public Guid MaterialId { get; set; }
    public virtual StudyMaterial Material { get; set; } = null!;

    public Guid StudentId { get; set; }
    public virtual User Student { get; set; } = null!;

    public int Rating { get; set; } // 1 - 5
    public string? Comment { get; set; }
}
