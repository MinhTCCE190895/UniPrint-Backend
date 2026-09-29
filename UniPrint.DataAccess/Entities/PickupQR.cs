namespace UniPrint.DataAccess.Entities;

public class PickupQR : BaseEntity
{
    public Guid OrderId { get; set; }
    public virtual PrintOrder Order { get; set; } = null!;

    public string QrToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime? UsedAt { get; set; }
}
