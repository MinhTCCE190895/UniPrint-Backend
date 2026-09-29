using UniPrint.DataAccess.Enums;

namespace UniPrint.DataAccess.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.Student;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    public virtual ICollection<PrintOrder> StudentOrders { get; set; } = new List<PrintOrder>();
    public virtual ICollection<PrintOrder> ProcessedOrders { get; set; } = new List<PrintOrder>();
}
