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
    private readonly Guid _staffId = Guid.NewGuid();

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
            },
            new User
            {
                Id = _staffId,
                FullName = "Printing Staff",
                Email = "staff@uniprint.edu.vn",
                PasswordHash = "hash",
                Role = UserRole.Staff
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
        result.Message.Should().Contain("thành công");
        var updatedOrder = await _dbContext.PrintOrders.FindAsync(order.Id);
        updatedOrder!.Status.Should().Be(PrintOrderStatus.Cancelled);
        updatedOrder.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderIsProcessing_ShouldCancelSuccessfully()
    {
        // Arrange - Trạng thái Processing: Staff đã tiếp nhận nhưng chưa chạy máy in (Printing)
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _studentId,
            FileName = "ProcessingCancel.pdf",
            FileExtension = ".pdf",
            FilePath = "uploads/processing.pdf",
            FileSizeBytes = 1000,
            PageCount = 5
        };
        await _dbContext.Documents.AddAsync(doc);

        var opt = new PrintOption
        {
            Id = Guid.NewGuid(),
            NumberOfCopies = 1,
            UnitPricePerPage = 350m,
            TotalPrice = 3500m
        };
        await _dbContext.PrintOptions.AddAsync(opt);

        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-PROCESSING-1",
            StudentId = _studentId,
            DocumentId = doc.Id,
            PrintOptionId = opt.Id,
            Status = PrintOrderStatus.Processing,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.CancelOrderAsync(order.Id, _studentId);

        // Assert - Cho phép hủy vì chưa in
        result.Success.Should().BeTrue();
        var updatedOrder = await _dbContext.PrintOrders.FindAsync(order.Id);
        updatedOrder!.Status.Should().Be(PrintOrderStatus.Cancelled);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderIsPrintingOrHigher_ShouldFail_DueToBR02()
    {
        // Arrange - Trạng thái Printing: Staff đã bắt đầu in -> Chặn hủy theo BR02
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

        // Assert - Phải chặn theo BR02
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("BR02");
        var updatedOrder = await _dbContext.PrintOrders.FindAsync(order.Id);
        updatedOrder!.Status.Should().Be(PrintOrderStatus.Printing);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderIsReadyForPickup_ShouldFail_DueToBR02()
    {
        // Arrange - Đã in xong xếp kệ (ReadyForPickup >= Printing) -> Chặn hủy
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _studentId,
            FileName = "ReadyCancelTest.pdf",
            FileExtension = ".pdf",
            FilePath = "uploads/ready.pdf",
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
            OrderCode = "ORD-READY-1",
            StudentId = _studentId,
            DocumentId = doc.Id,
            PrintOptionId = opt.Id,
            Status = PrintOrderStatus.ReadyForPickup,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.CancelOrderAsync(order.Id, _studentId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("BR02");
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderIsAlreadyCancelled_ShouldFail()
    {
        // Arrange
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _studentId,
            FileName = "AlreadyCancelled.pdf",
            FileExtension = ".pdf",
            FilePath = "uploads/already.pdf",
            FileSizeBytes = 1000,
            PageCount = 2
        };
        await _dbContext.Documents.AddAsync(doc);

        var opt = new PrintOption
        {
            Id = Guid.NewGuid(),
            NumberOfCopies = 1,
            UnitPricePerPage = 350m,
            TotalPrice = 700m
        };
        await _dbContext.PrintOptions.AddAsync(opt);

        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-ALREADY-CANCELLED",
            StudentId = _studentId,
            DocumentId = doc.Id,
            PrintOptionId = opt.Id,
            Status = PrintOrderStatus.Cancelled,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act - Hủy lần thứ hai
        var result = await _service.CancelOrderAsync(order.Id, _studentId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("đã bị hủy trước đó");
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderBelongsToAnotherStudent_ShouldReturnFail_PreventIDOR()
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

        // Act - Student cố tình hủy đơn của người khác
        var result = await _service.CancelOrderAsync(order.Id, _studentId);

        // Assert - Chặn IDOR
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không tìm thấy đơn hàng");
    }

    [Fact]
    public async Task CancelOrderAsync_WhenStaffCancelsForStudent_ShouldCancelSuccessfully()
    {
        // Arrange
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _studentId,
            FileName = "StaffCancelTest.pdf",
            FileExtension = ".pdf",
            FilePath = "uploads/staffcancel.pdf",
            FileSizeBytes = 1000,
            PageCount = 3
        };
        await _dbContext.Documents.AddAsync(doc);

        var opt = new PrintOption
        {
            Id = Guid.NewGuid(),
            NumberOfCopies = 1,
            UnitPricePerPage = 350m,
            TotalPrice = 1050m
        };
        await _dbContext.PrintOptions.AddAsync(opt);

        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-STAFF-CANCEL",
            StudentId = _studentId,
            DocumentId = doc.Id,
            PrintOptionId = opt.Id,
            Status = PrintOrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act - Staff hủy đơn hỗ trợ sinh viên tại quầy
        var result = await _service.CancelOrderAsync(order.Id, _staffId, isStaffOrAdmin: true);

        // Assert
        result.Success.Should().BeTrue();
        var updatedOrder = await _dbContext.PrintOrders.FindAsync(order.Id);
        updatedOrder!.Status.Should().Be(PrintOrderStatus.Cancelled);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderHasPendingPayment_ShouldMarkPaymentAsFailed()
    {
        // Arrange
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _studentId,
            FileName = "PendingPayment.pdf",
            FileExtension = ".pdf",
            FilePath = "uploads/payment.pdf",
            FileSizeBytes = 1000,
            PageCount = 3
        };
        await _dbContext.Documents.AddAsync(doc);

        var opt = new PrintOption
        {
            Id = Guid.NewGuid(),
            NumberOfCopies = 1,
            UnitPricePerPage = 350m,
            TotalPrice = 1050m
        };
        await _dbContext.PrintOptions.AddAsync(opt);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Amount = 1050m,
            Method = PaymentMethod.Cash,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.Payments.AddAsync(payment);

        var order = new PrintOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = "ORD-PAYMENT-CANCEL",
            StudentId = _studentId,
            DocumentId = doc.Id,
            PrintOptionId = opt.Id,
            Status = PrintOrderStatus.Pending,
            Payment = payment,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.PrintOrders.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.CancelOrderAsync(order.Id, _studentId);

        // Assert
        result.Success.Should().BeTrue();
        var updatedPayment = await _dbContext.Payments.FindAsync(payment.Id);
        updatedPayment!.Status.Should().Be(PaymentStatus.Failed);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
