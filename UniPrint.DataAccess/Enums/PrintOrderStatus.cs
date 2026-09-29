namespace UniPrint.DataAccess.Enums;

public enum PrintOrderStatus
{
    Pending = 1,
    Processing = 2,
    Printing = 3,
    ShelfAssigned = 4,
    ReadyForPickup = 5,
    Completed = 6,
    Cancelled = 7
}
