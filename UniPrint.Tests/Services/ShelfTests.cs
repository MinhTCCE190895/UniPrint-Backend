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

public class ShelfTests : IDisposable
{
    private readonly UniPrintDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ShelfService _shelfService;

    public ShelfTests()
    {
        var options = new DbContextOptionsBuilder<UniPrintDbContext>()
            .UseInMemoryDatabase(databaseName: $"UniPrint_ShelfTest_{Guid.NewGuid()}")
            .Options;

        _dbContext = new UniPrintDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);
        _shelfService = new ShelfService(_unitOfWork);
    }

    [Fact]
    public async Task GetAllShelvesAsync_ShouldReturnShelvesOrderedByShelfCode()
    {
        // Arrange
        var shelfB = new Shelf { Id = Guid.NewGuid(), ShelfCode = "B1", Location = "Khu B", MaxCapacity = 20, CurrentCount = 5 };
        var shelfA = new Shelf { Id = Guid.NewGuid(), ShelfCode = "A1", Location = "Khu A", MaxCapacity = 20, CurrentCount = 0 };
        await _dbContext.Shelves.AddRangeAsync(shelfB, shelfA);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _shelfService.GetAllShelvesAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Count.Should().Be(2);
        result.Data[0].ShelfCode.Should().Be("A1");
        result.Data[1].ShelfCode.Should().Be("B1");
    }

    [Fact]
    public async Task GetAvailableShelvesAsync_ShouldFilterOutFullShelves_EC06()
    {
        // Arrange
        var shelfAvailable = new Shelf { Id = Guid.NewGuid(), ShelfCode = "A1", Location = "Khu A", MaxCapacity = 20, CurrentCount = 15 };
        var shelfFull = new Shelf { Id = Guid.NewGuid(), ShelfCode = "A2", Location = "Khu A", MaxCapacity = 20, CurrentCount = 20 }; // Đầy (EC06)
        var shelfOverCapacity = new Shelf { Id = Guid.NewGuid(), ShelfCode = "A3", Location = "Khu A", MaxCapacity = 10, CurrentCount = 10 }; // Đầy
        await _dbContext.Shelves.AddRangeAsync(shelfAvailable, shelfFull, shelfOverCapacity);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _shelfService.GetAvailableShelvesAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Count.Should().Be(1);
        result.Data[0].ShelfCode.Should().Be("A1");
        result.Data[0].IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task CreateShelfAsync_ShouldSucceed_WhenValidInputProvided()
    {
        // Arrange
        var dto = new CreateShelfDto
        {
            ShelfCode = "c1",
            Location = "Tầng 2 - Phòng In",
            MaxCapacity = 25
        };

        // Act
        var result = await _shelfService.CreateShelfAsync(dto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ShelfCode.Should().Be("C1"); // Normalized to uppercase
        result.Data.Location.Should().Be("Tầng 2 - Phòng In");
        result.Data.MaxCapacity.Should().Be(25);
        result.Data.CurrentCount.Should().Be(0);
        result.Data.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task CreateShelfAsync_ShouldFail_WhenShelfCodeAlreadyExists()
    {
        // Arrange
        var existingShelf = new Shelf { Id = Guid.NewGuid(), ShelfCode = "A1", Location = "Khu A", MaxCapacity = 20, CurrentCount = 0 };
        await _dbContext.Shelves.AddAsync(existingShelf);
        await _dbContext.SaveChangesAsync();

        var dto = new CreateShelfDto { ShelfCode = "a1", Location = "Khu Khác", MaxCapacity = 20 };

        // Act
        var result = await _shelfService.CreateShelfAsync(dto);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("đã tồn tại");
    }

    [Fact]
    public async Task CreateShelfAsync_ShouldFail_WhenShelfCodeIsEmpty()
    {
        // Arrange
        var dto = new CreateShelfDto { ShelfCode = "   ", Location = "Khu A" };

        // Act
        var result = await _shelfService.CreateShelfAsync(dto);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không được để trống");
    }

    [Fact]
    public async Task UpdateShelfAsync_ShouldFail_WhenViolatingEC06_MaxCapacityLessThanCurrentCount()
    {
        // Arrange: Kệ đang có 12 đơn hàng
        var shelf = new Shelf
        {
            Id = Guid.NewGuid(),
            ShelfCode = "A1",
            Location = "Khu A",
            MaxCapacity = 20,
            CurrentCount = 12
        };
        await _dbContext.Shelves.AddAsync(shelf);
        await _dbContext.SaveChangesAsync();

        // Cố tình giảm MaxCapacity xuống 8 (< CurrentCount = 12)
        var updateDto = new UpdateShelfDto
        {
            ShelfCode = "A1",
            Location = "Khu A Mới",
            MaxCapacity = 8
        };

        // Act
        var result = await _shelfService.UpdateShelfAsync(shelf.Id, updateDto);

        // Assert: Chặn giảm dung tích dưới mức đang chứa
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không thể giảm sức chứa");

        // Đảm bảo dữ liệu trong database không bị thay đổi
        var dbShelf = await _dbContext.Shelves.FindAsync(shelf.Id);
        dbShelf!.MaxCapacity.Should().Be(20);
    }

    [Fact]
    public async Task UpdateShelfAsync_ShouldSucceed_WhenMaxCapacityGreaterThanOrEqualToCurrentCount()
    {
        // Arrange
        var shelf = new Shelf
        {
            Id = Guid.NewGuid(),
            ShelfCode = "A1",
            Location = "Khu A",
            MaxCapacity = 20,
            CurrentCount = 5
        };
        await _dbContext.Shelves.AddAsync(shelf);
        await _dbContext.SaveChangesAsync();

        var updateDto = new UpdateShelfDto
        {
            ShelfCode = "A1-PRO",
            Location = "Khu A Mới",
            MaxCapacity = 10
        };

        // Act
        var result = await _shelfService.UpdateShelfAsync(shelf.Id, updateDto);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ShelfCode.Should().Be("A1-PRO");
        result.Data.MaxCapacity.Should().Be(10);
    }

    [Fact]
    public async Task DeleteShelfAsync_ShouldFail_WhenShelfHasActiveOrders_EC06()
    {
        // Arrange: Kệ đang chứa 3 tài liệu
        var shelf = new Shelf
        {
            Id = Guid.NewGuid(),
            ShelfCode = "B1",
            Location = "Khu B",
            MaxCapacity = 20,
            CurrentCount = 3
        };
        await _dbContext.Shelves.AddAsync(shelf);
        await _dbContext.SaveChangesAsync();

        // Act: Xóa kệ đang có tài liệu
        var result = await _shelfService.DeleteShelfAsync(shelf.Id);

        // Assert: Chặn xóa kệ đang chứa tài liệu
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không thể xóa");

        // Kệ vẫn còn trong database
        var dbShelf = await _dbContext.Shelves.FindAsync(shelf.Id);
        dbShelf.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteShelfAsync_ShouldSucceed_WhenShelfIsEmpty()
    {
        // Arrange: Kệ rỗng
        var shelf = new Shelf
        {
            Id = Guid.NewGuid(),
            ShelfCode = "EMPTY1",
            Location = "Khu Rỗng",
            MaxCapacity = 20,
            CurrentCount = 0
        };
        await _dbContext.Shelves.AddAsync(shelf);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _shelfService.DeleteShelfAsync(shelf.Id);

        // Assert
        result.Success.Should().BeTrue();

        var dbShelf = await _dbContext.Shelves.FindAsync(shelf.Id);
        dbShelf.Should().BeNull();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _unitOfWork.Dispose();
    }
}
