using UniPrint.DataAccess.Enums;

namespace UniPrint.DataAccess.Entities;

public class PrintOrder : BaseEntity
{
    public string OrderCode { get; set; } = string.Empty; // e.g. "ORD-20260929-XXXX"
    
    public Guid StudentId { get; set; }
    public virtual User Student { get; set; } = null!;

    public Guid DocumentId { get; set; }
    public virtual Document Document { get; set; } = null!;

    public Guid PrintOptionId { get; set; }
    public virtual PrintOption PrintOption { get; set; } = null!;

    public Guid? StaffId { get; set; }
    public virtual User? Staff { get; set; }

    public Guid? ShelfId { get; set; }
    public virtual Shelf? Shelf { get; set; }

    public PrintOrderStatus Status { get; set; } = PrintOrderStatus.Pending;
    public DateTime? EstimatedPickupTime { get; set; }
    public string? Notes { get; set; }

    public virtual PickupQR? PickupQR { get; set; }
    public virtual Payment? Payment { get; set; }
}
