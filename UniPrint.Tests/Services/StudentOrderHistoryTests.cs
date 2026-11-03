using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UniPrint.Business.Common;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;
using Xunit;

namespace UniPrint.Tests.Services;

public class StudentOrderHistoryTests : IDisposable
{
    private readonly UniPrintDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Mock<IPriceCalculatorService> _mockPriceCalculator;
    private readonly PrintOrderService _service;
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _otherStudentId = Guid.NewGuid();

    public StudentOrderHistoryTests()
    {
        var options = new DbContextOptionsBuilder<UniPrintDbContext>()
            .UseInMemoryDatabase(databaseName: $"UniPrint_OrderHistory_Test_{Guid.NewGuid()}")
            .Options;

        _dbContext = new UniPrintDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);
        _mockPriceCalculator = new Mock<IPriceCalculatorService>();
        _service = new PrintOrderService(_unitOfWork, _mockPriceCalculator.Object);

        // Seed users
        _dbContext.Users.AddRange(
            new User
            {
                Id = _studentId,
                FullName = "Tran Huynh Khoi",
                Email = "khoith@uniprint.edu.vn",
                PasswordHash = "hash",
                Role = UserRole.Student
            },
            new User
            {
                Id = _otherStudentId,
                FullName = "Other Student",
                Email = "other@uniprint.edu.vn",
                PasswordHash = "hash",
                Role = UserRole.Student
            }
        );
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetStudentOrdersAsync_ReturnsOnlyStudentOrders_OrderedByCreatedAtDesc()
    {
        // Arrange
        var doc1 = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _studentId,
            FileName = "Doc1.pdf",
            FileExtension = ".pdf",
            FilePath = "uploads/doc1.pdf",
            FileSizeBytes = 1000,
            PageCount = 10
        };
        var doc2 = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _otherStudentId,
            FileName = "Doc2.pdf",
            FileExtension = ".pdf",
            FilePath = "uploads/doc2.pdf",
            FileSizeBytes = 2000,
            PageCount = 20
        };
        await _dbContext.Documents.AddRangeAsync(doc1, doc2);

        var opt1 = new PrintOption
        {
            Id = Guid.NewGuid(),
            NumberOfCopies = 1,
            UnitPricePerPage = 350m,
            TotalPrice = 5000m
        };
        var opt2 = new PrintOption
        {
            Id = Guid.NewGuid(),
            NumberOfCopies = 1,
            UnitPricePerPage = 350m,
            TotalPrice = 10000m
        };
        var opt3 = new PrintOption
        {
            Id = Guid.NewGuid(),
            NumberOfCopies = 1,
            UnitPricePerPage = 350m,
            TotalPrice = 8000m
        };
        await _dbContext.PrintOptions.AddRangeAsync(opt1, opt2, opt3);

        var order1 = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-000001",
            StudentId = _studentId,
            DocumentId = doc1.Id,
            PrintOptionId = opt1.Id,
            Status = PrintOrderStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var order2 = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-000002",
            StudentId = _studentId,
            DocumentId = doc1.Id,
            PrintOptionId = opt2.Id,
            Status = PrintOrderStatus.ReadyForPickup,
            CreatedAt = DateTime.UtcNow
        };
        var otherOrder = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-000003",
            StudentId = _otherStudentId,
            DocumentId = doc2.Id,
            PrintOptionId = opt3.Id,
            Status = PrintOrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.PrintOrders.AddRangeAsync(order1, order2, otherOrder);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetStudentOrdersAsync(_studentId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Count.Should().Be(2);
        result.Data[0].OrderCode.Should().Be("ORD-000002"); // Descending order
        result.Data[1].OrderCode.Should().Be("ORD-000001");
        result.Data.Should().NotContain(o => o.OrderCode == "ORD-000003");
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderIsPending_ShouldCancelSuccessfully()
    {
        // Arrange
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _studentId,
            FileName = "CancelTest.pdf",
            FileExtension = ".pdf",
            FilePath = "uploads/cancel.pdf",
            FileSizeBytes = 1000,
            PageCount = 5
        };
        await _dbContext.Documents.AddAsync(doc);

        var opt = new PrintOption
        {
            Id = Guid.NewGuid(),
            NumberOfCopies = 1,
            UnitPricePerPage = 350m,
            TotalPrice = 3000m
        };
        await _dbContext.PrintOptions.AddAsync(opt);

        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-CANCEL-1",
            StudentId = _studentId,
            DocumentId = doc.Id,
            PrintOptionId = opt.Id,
            Status = PrintOrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.CancelOrderAsync(order.Id, _studentId);

        // Assert
        result.Success.Should().BeTrue();
        var updatedOrder = await _dbContext.PrintOrders.FindAsync(order.Id);
        updatedOrder!.Status.Should().Be(PrintOrderStatus.Cancelled);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderIsPrintingOrHigher_ShouldFail_DueToBR02()
    {
        // Arrange
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _studentId,
            FileName = "LockTest.pdf",
            FileExtension = ".pdf",
            FilePath = "uploads/lock.pdf",
            FileSizeBytes = 1000,
            PageCount = 5
        };
        await _dbContext.Documents.AddAsync(doc);

        var opt = new PrintOption
        {
            Id = Guid.NewGuid(),
            NumberOfCopies = 1,
            UnitPricePerPage = 350m,
            TotalPrice = 5000m
        };
        await _dbContext.PrintOptions.AddAsync(opt);

        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-PRINTING-1",
            StudentId = _studentId,
            DocumentId = doc.Id,
            PrintOptionId = opt.Id,
            Status = PrintOrderStatus.Printing,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.CancelOrderAsync(order.Id, _studentId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("BR02");
        var updatedOrder = await _dbContext.PrintOrders.FindAsync(order.Id);
        updatedOrder!.Status.Should().Be(PrintOrderStatus.Printing);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderBelongsToAnotherStudent_ShouldReturnFail()
    {
        // Arrange
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _otherStudentId,
            FileName = "OtherDoc.pdf",
            FileExtension = ".pdf",
            FilePath = "uploads/other.pdf",
            FileSizeBytes = 1000,
            PageCount = 5
        };
        await _dbContext.Documents.AddAsync(doc);

        var opt = new PrintOption
        {
            Id = Guid.NewGuid(),
            NumberOfCopies = 1,
            UnitPricePerPage = 350m,
            TotalPrice = 4000m
        };
        await _dbContext.PrintOptions.AddAsync(opt);

        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-OTHER-1",
            StudentId = _otherStudentId,
            DocumentId = doc.Id,
            PrintOptionId = opt.Id,
            Status = PrintOrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act - Student tries to cancel someone else's order
        var result = await _service.CancelOrderAsync(order.Id, _studentId);

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
