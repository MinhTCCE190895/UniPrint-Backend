namespace UniPrint.DataAccess.Entities;

public class PrintOption : BaseEntity
{
    public int NumberOfCopies { get; set; } = 1;
    public bool IsColor { get; set; } = false;
    public bool IsDoubleSided { get; set; } = false;
    public bool HasBinding { get; set; } = false;
    public bool HasStaple { get; set; } = false;
    public decimal UnitPricePerPage { get; set; }
    public decimal TotalPrice { get; set; }
}
