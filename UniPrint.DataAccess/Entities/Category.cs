namespace UniPrint.DataAccess.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty; // e.g. "Slide Bài Giảng", "Đề Thi & Đáp Án", "Tóm Tắt Ôn Tập"
    public string? Description { get; set; }
    public virtual ICollection<StudyMaterial> Materials { get; set; } = new List<StudyMaterial>();
}
