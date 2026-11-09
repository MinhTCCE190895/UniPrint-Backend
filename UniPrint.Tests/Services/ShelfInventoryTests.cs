using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;
using Xunit;

namespace UniPrint.Tests.Services;

public class ShelfInventoryTests : IDisposable
{
    private readonly UniPrintDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ShelfService _shelfService;

    public ShelfInventoryTests()
    {
        var options = new DbContextOptionsBuilder<UniPrintDbContext>()
            .UseInMemoryDatabase(databaseName: $"UniPrint_ShelfInventoryTest_{Guid.NewGuid()}")
            .Options;

        _dbContext = new UniPrintDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);
        _shelfService = new ShelfService(_unitOfWork);
    }

    [Fact]
    public async Task GetShelfInventoryAsync_ShouldCalculateSummaryMetricsCorrectly()
    {
        // Arrange: 1 shelf with 20/20 (full), 1 shelf with 5/20 (available)
        var shelfFull = new Shelf { Id = Guid.NewGuid(), ShelfCode = "A1", Location = "Tầng 1", MaxCapacity = 20, CurrentCount = 20 };
        var shelfAvail = new Shelf { Id = Guid.NewGuid(), ShelfCode = "A2", Location = "Tầng 1", MaxCapacity = 20, CurrentCount = 5 };
        await _dbContext.Shelves.AddRangeAsync(shelfFull, shelfAvail);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _shelfService.GetShelfInventoryAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        var summary = result.Data!.Summary;
        summary.TotalShelves.Should().Be(2);
        summary.TotalCapacity.Should().Be(40);
        summary.TotalStoredOrders.Should().Be(25);
        summary.AvailableShelvesCount.Should().Be(1);
        summary.FullShelvesCount.Should().Be(1);
        summary.OverallOccupancyRate.Should().Be(62.5); // 25 / 40 * 100 = 62.5%
        result.Data.Shelves.Count.Should().Be(2);
    }

    [Fact]
    public async Task GetShelfInventoryAsync_ShouldIncludeActiveStoredOrdersAndExcludeCompletedOrCancelled()
    {
        // Arrange
        var shelf = new Shelf { Id = Guid.NewGuid(), ShelfCode = "B1", Location = "Khu B", MaxCapacity = 15, CurrentCount = 1 };
        var student = new User { Id = Guid.NewGuid(), FullName = "Nguyen Van A", Email = "a@uni.edu.vn", Role = UserRole.Student };
        var doc = new Document { Id = Guid.NewGuid(), FileName = "BaoCao.pdf", FilePath = "uploads/bc.pdf", FileExtension = ".pdf", FileSizeBytes = 1024, PageCount = 10, UserId = student.Id };
        var printOpt = new PrintOption { Id = Guid.NewGuid(), NumberOfCopies = 2, IsColor = true, IsDoubleSided = false, TotalPrice = 15000 };

        var activeOrder = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-001",
            StudentId = student.Id,
            DocumentId = doc.Id,
            PrintOptionId = printOpt.Id,
            ShelfId = shelf.Id,
            Status = PrintOrderStatus.ReadyForPickup,
            CreatedAt = DateTime.UtcNow
        };

        var completedOrder = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-002",
            StudentId = student.Id,
            DocumentId = doc.Id,
            PrintOptionId = printOpt.Id,
            ShelfId = shelf.Id,
            Status = PrintOrderStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        var cancelledOrder = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-003",
            StudentId = student.Id,
            DocumentId = doc.Id,
            PrintOptionId = printOpt.Id,
            ShelfId = shelf.Id,
            Status = PrintOrderStatus.Cancelled,
            CreatedAt = DateTime.UtcNow.AddMinutes(-60)
        };

        await _dbContext.Shelves.AddAsync(shelf);
        await _dbContext.Users.AddAsync(student);
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.PrintOptions.AddAsync(printOpt);
        await _dbContext.PrintOrders.AddRangeAsync(activeOrder, completedOrder, cancelledOrder);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _shelfService.GetShelfInventoryAsync();

        // Assert
        result.Success.Should().BeTrue();
        var shelfItem = result.Data!.Shelves.First(s => s.Id == shelf.Id);
        shelfItem.StoredOrders.Should().HaveCount(1);
        shelfItem.StoredOrders[0].OrderCode.Should().Be("ORD-001");
        shelfItem.StoredOrders[0].StudentName.Should().Be("Nguyen Van A");
        shelfItem.StoredOrders[0].DocumentName.Should().Be("BaoCao.pdf");
        shelfItem.StoredOrders[0].TotalPrice.Should().Be(15000);
        shelfItem.StoredOrders[0].IsColor.Should().BeTrue();
    }

    [Fact]
    public async Task GetShelfInventoryAsync_WhenNoShelves_ShouldReturnEmptySummary()
    {
        // Act
        var result = await _shelfService.GetShelfInventoryAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Summary.TotalShelves.Should().Be(0);
        result.Data.Summary.TotalCapacity.Should().Be(0);
        result.Data.Summary.TotalStoredOrders.Should().Be(0);
        result.Data.Summary.OverallOccupancyRate.Should().Be(0);
        result.Data.Shelves.Should().BeEmpty();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
