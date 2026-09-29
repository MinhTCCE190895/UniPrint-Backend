namespace UniPrint.DataAccess.Entities;

public class Subject : BaseEntity
{
    public string Code { get; set; } = string.Empty; // e.g. "PRN231"
    public string Name { get; set; } = string.Empty;
    public virtual ICollection<StudyMaterial> Materials { get; set; } = new List<StudyMaterial>();
}
