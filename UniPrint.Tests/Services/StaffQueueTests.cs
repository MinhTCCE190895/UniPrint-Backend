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

public class StaffQueueTests : IDisposable
{
    private readonly UniPrintDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Mock<IPriceCalculatorService> _mockPriceCalculator;
    private readonly PrintOrderService _service;

    public StaffQueueTests()
    {
        var options = new DbContextOptionsBuilder<UniPrintDbContext>()
            .UseInMemoryDatabase(databaseName: $"UniPrint_Test_{Guid.NewGuid()}")
            .Options;

        _dbContext = new UniPrintDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);
        _mockPriceCalculator = new Mock<IPriceCalculatorService>();
        _service = new PrintOrderService(_unitOfWork, _mockPriceCalculator.Object);
    }

    [Fact]
    public async Task GetStaffQueueOrdersAsync_ShouldReturnOrdersInFifoOrder_ExcludingCompletedAndCancelled()
    {
        // Arrange: Tạo student & documents mẫu
        var student = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Nguyen Cao Qui",
            Email = "quinc@uniprint.edu.vn",
            PasswordHash = "hash",
            Role = UserRole.Student
        };
        await _dbContext.Users.AddAsync(student);

        var doc1 = new Document { Id = Guid.NewGuid(), UserId = student.Id, FileName = "TieuLuan.pdf", PageCount = 10, FileSizeBytes = 1024, FileExtension = ".pdf" };
        var doc2 = new Document { Id = Guid.NewGuid(), UserId = student.Id, FileName = "BaoCao.docx", PageCount = 5, FileSizeBytes = 2048, FileExtension = ".docx" };
        var doc3 = new Document { Id = Guid.NewGuid(), UserId = student.Id, FileName = "DonHuy.pdf", PageCount = 2, FileSizeBytes = 512, FileExtension = ".pdf" };
        var doc4 = new Document { Id = Guid.NewGuid(), UserId = student.Id, FileName = "DonHoanTat.pdf", PageCount = 20, FileSizeBytes = 4096, FileExtension = ".pdf" };
        await _dbContext.Documents.AddRangeAsync(doc1, doc2, doc3, doc4);

        var opt = new PrintOption { Id = Guid.NewGuid(), NumberOfCopies = 1, UnitPricePerPage = 350, TotalPrice = 3500 };
        await _dbContext.PrintOptions.AddAsync(opt);

        // Tạo 4 đơn hàng: Đơn cũ hơn (T1), Đơn mới hơn (T2), Đơn Cancelled, Đơn Completed
        var baseTime = DateTime.UtcNow;
        var orderOlder = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-001",
            StudentId = student.Id,
            DocumentId = doc1.Id,
            PrintOption = opt,
            Status = PrintOrderStatus.Pending,
            CreatedAt = baseTime.AddMinutes(-30) // Đặt trước 30 phút
        };

        var orderNewer = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-002",
            StudentId = student.Id,
            DocumentId = doc2.Id,
            PrintOption = opt,
            Status = PrintOrderStatus.Processing,
            CreatedAt = baseTime.AddMinutes(-10) // Đặt trước 10 phút
        };

        var orderCancelled = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-CANCELLED",
            StudentId = student.Id,
            DocumentId = doc3.Id,
            PrintOption = opt,
            Status = PrintOrderStatus.Cancelled,
            CreatedAt = baseTime.AddMinutes(-50)
        };

        var orderCompleted = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-COMPLETED",
            StudentId = student.Id,
            DocumentId = doc4.Id,
            PrintOption = opt,
            Status = PrintOrderStatus.Completed,
            CreatedAt = baseTime.AddMinutes(-60)
        };

        await _dbContext.PrintOrders.AddRangeAsync(orderOlder, orderNewer, orderCancelled, orderCompleted);
        await _dbContext.SaveChangesAsync();

        // Act: Task 11 GetStaffQueueOrdersAsync
        var response = await _service.GetStaffQueueOrdersAsync();

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Count.Should().Be(2);

        // Kiểm tra đúng nguyên lý FIFO (Đơn cũ hơn xếp trước)
        response.Data[0].OrderCode.Should().Be("ORD-001");
        response.Data[1].OrderCode.Should().Be("ORD-002");

        // Đảm bảo không chứa đơn Cancelled hay Completed
        response.Data.Any(o => o.OrderCode == "ORD-CANCELLED").Should().BeFalse();
        response.Data.Any(o => o.OrderCode == "ORD-COMPLETED").Should().BeFalse();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _unitOfWork.Dispose();
    }
}
