namespace UniPrint.DataAccess.Entities;

public class Favorite : BaseEntity
{
    public Guid StudentId { get; set; }
    public virtual User Student { get; set; } = null!;

    public Guid MaterialId { get; set; }
    public virtual StudyMaterial Material { get; set; } = null!;
}
