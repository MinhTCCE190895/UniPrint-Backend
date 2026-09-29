namespace UniPrint.DataAccess.Entities;

public class Shelf : BaseEntity
{
    public string ShelfCode { get; set; } = string.Empty; // e.g. "A1", "B2"
    public string Location { get; set; } = "Main Library Floor 1";
    public int MaxCapacity { get; set; } = 20;
    public int CurrentCount { get; set; } = 0;
    public bool IsAvailable => CurrentCount < MaxCapacity;

    public virtual ICollection<PrintOrder> Orders { get; set; } = new List<PrintOrder>();
}
