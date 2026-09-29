using UniPrint.DataAccess.Enums;

namespace UniPrint.DataAccess.Entities;

public class Payment : BaseEntity
{
    public Guid OrderId { get; set; }
    public virtual PrintOrder Order { get; set; } = null!;

    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? TransactionCode { get; set; }
    public DateTime? PaidAt { get; set; }
}
