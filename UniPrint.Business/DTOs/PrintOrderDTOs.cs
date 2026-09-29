using UniPrint.DataAccess.Enums;

namespace UniPrint.Business.DTOs;

public class DocumentUploadDto
{
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int PageCount { get; set; } = 1;
    public string TempStoragePath { get; set; } = string.Empty;
}

public class DocumentResponseDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int PageCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CalculatePriceRequestDto
{
    public int PageCount { get; set; }
    public int NumberOfCopies { get; set; } = 1;
    public bool IsColor { get; set; } = false;
    public bool IsDoubleSided { get; set; } = false;
    public bool HasBinding { get; set; } = false;
    public bool HasStaple { get; set; } = false;
}

public class PriceCalculationResultDto
{
    public decimal UnitPricePerPage { get; set; }
    public decimal PageSubtotal { get; set; }
    public decimal ExtraServicesTotal { get; set; }
    public decimal TotalEstimatedPrice { get; set; }
}

public class CreatePrintOrderDto
{
    public Guid DocumentId { get; set; }
    public int NumberOfCopies { get; set; } = 1;
    public bool IsColor { get; set; } = false;
    public bool IsDoubleSided { get; set; } = false;
    public bool HasBinding { get; set; } = false;
    public bool HasStaple { get; set; } = false;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }
}

public class PrintOrderResponseDto
{
    public Guid Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public int NumberOfCopies { get; set; }
    public bool IsColor { get; set; }
    public bool IsDoubleSided { get; set; }
    public decimal TotalPrice { get; set; }
    public PrintOrderStatus Status { get; set; }
    public string StatusText => Status.ToString();
    public string? ShelfCode { get; set; }
    public string? QrToken { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateOrderStatusDto
{
    public PrintOrderStatus NewStatus { get; set; }
}

public class AssignShelfDto
{
    public Guid ShelfId { get; set; }
}
