namespace UniPrint.DataAccess.Entities;

public class PriceConfig : BaseEntity
{
    public decimal PricePerBWPage { get; set; } = 300m;
    public decimal PricePerColorPage { get; set; } = 1500m;
    public decimal DoubleSidedDiscountPercent { get; set; } = 10m;
    public decimal BindingPrice { get; set; } = 5000m;
    public decimal StaplePrice { get; set; } = 1000m;
    public bool IsActive { get; set; } = true;
}
