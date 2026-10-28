using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;
using Xunit;

namespace UniPrint.Tests.Services;

public class AssignShelfTests : IDisposable
{
    private readonly UniPrintDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Mock<IPriceCalculatorService> _mockPriceCalculator;
    private readonly PrintOrderService _service;
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();

    public AssignShelfTests()
    {
        var options = new DbContextOptionsBuilder<UniPrintDbContext>()
            .UseInMemoryDatabase(databaseName: $"UniPrint_AssignShelf_Test_{Guid.NewGuid()}")
            .Options;

        _dbContext = new UniPrintDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);
        _mockPriceCalculator = new Mock<IPriceCalculatorService>();
        _service = new PrintOrderService(_unitOfWork, _mockPriceCalculator.Object);

        var student = new User { Id = _studentId, FullName = "Nguyen Cao Qui", Email = "qui@uniprint.edu.vn", PasswordHash = "p", Role = UserRole.Student };
        var staff = new User { Id = _staffId, FullName = "Staff QuiNC", Email = "staff@uniprint.edu.vn", PasswordHash = "p", Role = UserRole.Staff };
        _dbContext.Users.AddRange(student, staff);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task AssignShelf_Valid_ShouldAssignShelfAndIncrementCurrentCount_AndSetReadyForPickup()
    {
        // Arrange
        var shelf = new Shelf
        {
            Id = Guid.NewGuid(),
            ShelfCode = "A1",
            Location = "Tầng 1 - Thư viện chính",
            MaxCapacity = 20,
            CurrentCount = 0
        };
        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-SHELF-01",
            StudentId = _studentId,
            DocumentId = Guid.NewGuid(),
            Status = PrintOrderStatus.Printing,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.Shelves.AddAsync(shelf);
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.AssignShelfAsync(order.Id, _staffId, shelf.Id);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("A1");

        var updatedOrder = await _dbContext.PrintOrders.FindAsync(order.Id);
        updatedOrder!.ShelfId.Should().Be(shelf.Id);
        updatedOrder.Status.Should().Be(PrintOrderStatus.ReadyForPickup);
        updatedOrder.StaffId.Should().Be(_staffId);

        var updatedShelf = await _dbContext.Shelves.FindAsync(shelf.Id);
        updatedShelf!.CurrentCount.Should().Be(1);
    }

    [Fact]
    public async Task AssignShelf_FullShelf_ShouldFail_DueToCapacityLimit()
    {
        // Arrange: Kệ đã đạt tối đa sức chứa (EC06)
        var fullShelf = new Shelf
        {
            Id = Guid.NewGuid(),
            ShelfCode = "B1",
            MaxCapacity = 20,
            CurrentCount = 20
        };
        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-SHELF-02",
            StudentId = _studentId,
            DocumentId = Guid.NewGuid(),
            Status = PrintOrderStatus.Printing,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.Shelves.AddAsync(fullShelf);
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.AssignShelfAsync(order.Id, _staffId, fullShelf.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("đầy chỗ");

        var untouchedOrder = await _dbContext.PrintOrders.FindAsync(order.Id);
        untouchedOrder!.ShelfId.Should().BeNull();
        untouchedOrder.Status.Should().Be(PrintOrderStatus.Printing);
    }

    [Fact]
    public async Task AssignShelf_OrderAlreadyHasShelf_ShouldFail()
    {
        // Arrange
        var shelf = new Shelf { Id = Guid.NewGuid(), ShelfCode = "A2", MaxCapacity = 20, CurrentCount = 1 };
        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-SHELF-03",
            StudentId = _studentId,
            DocumentId = Guid.NewGuid(),
            ShelfId = Guid.NewGuid(), // Đã có kệ
            Status = PrintOrderStatus.ReadyForPickup,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.Shelves.AddAsync(shelf);
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.AssignShelfAsync(order.Id, _staffId, shelf.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Đơn hàng đã có kệ");
    }

    [Fact]
    public async Task AssignShelf_InvalidStatus_ShouldFail()
    {
        // Arrange: Đơn hàng còn Pending (chưa in)
        var shelf = new Shelf { Id = Guid.NewGuid(), ShelfCode = "A3", MaxCapacity = 20, CurrentCount = 0 };
        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-SHELF-04",
            StudentId = _studentId,
            DocumentId = Guid.NewGuid(),
            Status = PrintOrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.Shelves.AddAsync(shelf);
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.AssignShelfAsync(order.Id, _staffId, shelf.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("chưa ở trạng thái cho phép gán kệ");
    }

    [Fact]
    public async Task AssignShelf_NonExistentShelf_ShouldFail()
    {
        // Arrange
        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-SHELF-05",
            StudentId = _studentId,
            DocumentId = Guid.NewGuid(),
            Status = PrintOrderStatus.Printing,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.AssignShelfAsync(order.Id, _staffId, Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không tồn tại");
    }

    [Fact]
    public async Task AssignShelf_NonExistentOrder_ShouldFail()
    {
        // Arrange
        var shelf = new Shelf { Id = Guid.NewGuid(), ShelfCode = "A4", MaxCapacity = 20, CurrentCount = 0 };
        await _dbContext.Shelves.AddAsync(shelf);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.AssignShelfAsync(Guid.NewGuid(), _staffId, shelf.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không tìm thấy đơn hàng");
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
