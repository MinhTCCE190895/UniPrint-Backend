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
