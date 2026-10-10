using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;
using Xunit;

namespace UniPrint.Tests.Services;

public class UpdatePrintingStatusTests : IDisposable
{
    private readonly UniPrintDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Mock<IPriceCalculatorService> _mockPriceCalculator;
    private readonly PrintOrderService _service;
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();

    public UpdatePrintingStatusTests()
    {
        var options = new DbContextOptionsBuilder<UniPrintDbContext>()
            .UseInMemoryDatabase(databaseName: $"UniPrint_Status_Test_{Guid.NewGuid()}")
            .Options;

        _dbContext = new UniPrintDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);
        _mockPriceCalculator = new Mock<IPriceCalculatorService>();
        _service = new PrintOrderService(_unitOfWork, _mockPriceCalculator.Object);

        // Seed Users
        var student = new User { Id = _studentId, FullName = "Nguyen Cao Qui", Email = "qui@uniprint.edu.vn", PasswordHash = "p", Role = UserRole.Student };
        var staff = new User { Id = _staffId, FullName = "Staff Member", Email = "staff@uniprint.edu.vn", PasswordHash = "p", Role = UserRole.Staff };
        _dbContext.Users.AddRange(student, staff);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldTransitionFromPendingToPrinting_AndEnforceBR02CancelLock()
    {
        // Arrange: Đơn hàng ở trạng thái Pending
        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-STATUS-01",
            StudentId = _studentId,
            DocumentId = Guid.NewGuid(),
            Status = PrintOrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // 1. Sinh viên có thể hủy khi còn Pending (BR02)
        // (Kiểm tra điều kiện trước khi chuyển Printing)

        // 2. Act: Staff nhận đơn và bắt đầu in -> Chuyển sang Printing (Task 12)
        var updateResult = await _service.UpdateOrderStatusAsync(order.Id, _staffId, PrintOrderStatus.Printing);

        // Assert update thành công
        updateResult.Success.Should().BeTrue();
        updateResult.Message.Should().Contain("Printing");

        var updatedOrder = await _dbContext.PrintOrders.FindAsync(order.Id);
        updatedOrder!.Status.Should().Be(PrintOrderStatus.Printing);
        updatedOrder.StaffId.Should().Be(_staffId);

        // 3. Assert Ràng buộc BR02: Sau khi đã Printing, sinh viên không được phép hủy đơn
        var cancelResult = await _service.CancelOrderAsync(order.Id, _studentId);
        cancelResult.Success.Should().BeFalse();
        cancelResult.Message.Should().Contain("BR02");
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldFail_WhenOrderIsAlreadyCancelledOrCompleted()
    {
        // Arrange
        var cancelledOrder = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-CANCELLED-01",
            StudentId = _studentId,
            DocumentId = Guid.NewGuid(),
            Status = PrintOrderStatus.Cancelled,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PrintOrders.AddAsync(cancelledOrder);
        await _dbContext.SaveChangesAsync();

        // Act: Cố cập nhật trạng thái đơn đã hủy
        var result = await _service.UpdateOrderStatusAsync(cancelledOrder.Id, _staffId, PrintOrderStatus.Printing);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("đã bị hủy");
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldFail_WhenInvalidTransitionRequested()
    {
        // Arrange: Đơn Pending không thể nhảy thẳng sang ReadyForPickup mà không qua in/gán kệ
        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-INVALID-TRANS",
            StudentId = _studentId,
            DocumentId = Guid.NewGuid(),
            Status = PrintOrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act: Chuyển Pending -> Completed (không hợp lệ)
        var result = await _service.UpdateOrderStatusAsync(order.Id, _staffId, PrintOrderStatus.Completed);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không thể chuyển trạng thái");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _unitOfWork.Dispose();
    }
}
