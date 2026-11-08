namespace UniPrint.Business.DTOs;

public class ShelfResponseDto
{
    public Guid Id { get; set; }
    public string ShelfCode { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int MaxCapacity { get; set; } = 20;
    public int CurrentCount { get; set; }
    public bool IsAvailable => CurrentCount < MaxCapacity;
    public double OccupancyRate => MaxCapacity > 0 ? Math.Round((double)CurrentCount / MaxCapacity * 100, 1) : 0;
    public DateTime CreatedAt { get; set; }
}

public class ShelfDetailDto : ShelfResponseDto
{
    public List<PrintOrderResponseDto> Orders { get; set; } = new();
}

public class CreateShelfDto
{
    public string ShelfCode { get; set; } = string.Empty;
    public string Location { get; set; } = "Main Library Floor 1";
    public int MaxCapacity { get; set; } = 20;
}

public class UpdateShelfDto
{
    public string ShelfCode { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int MaxCapacity { get; set; } = 20;
}

public class ShelfInventoryOrderDto
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public int NumberOfCopies { get; set; }
    public bool IsColor { get; set; }
    public bool IsDoubleSided { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ShelfInventoryItemDto : ShelfResponseDto
{
    public List<ShelfInventoryOrderDto> StoredOrders { get; set; } = new();
}

public class ShelfInventorySummaryDto
{
    public int TotalShelves { get; set; }
    public int TotalCapacity { get; set; }
    public int TotalStoredOrders { get; set; }
    public int AvailableShelvesCount { get; set; }
    public int FullShelvesCount { get; set; }
    public double OverallOccupancyRate { get; set; }
}

public class ShelfInventoryResponseDto
{
    public ShelfInventorySummaryDto Summary { get; set; } = new();
    public List<ShelfInventoryItemDto> Shelves { get; set; } = new();
}
