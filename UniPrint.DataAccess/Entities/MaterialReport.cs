namespace UniPrint.DataAccess.Entities;

public class MaterialReport : BaseEntity
{
    public Guid MaterialId { get; set; }
    public virtual StudyMaterial Material { get; set; } = null!;

    public Guid ReportedByStudentId { get; set; }
    public virtual User ReportedByStudent { get; set; } = null!;

    public string Reason { get; set; } = string.Empty;
    public bool IsResolved { get; set; } = false;
}
