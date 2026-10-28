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

public class CreatePrintOrderTests : IDisposable
{
    private readonly UniPrintDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Mock<IPriceCalculatorService> _mockPriceCalculator;
    private readonly PrintOrderService _service;
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _otherStudentId = Guid.NewGuid();

    public CreatePrintOrderTests()
    {
        var options = new DbContextOptionsBuilder<UniPrintDbContext>()
            .UseInMemoryDatabase(databaseName: $"UniPrint_CreateOrder_Test_{Guid.NewGuid()}")
            .Options;

        _dbContext = new UniPrintDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);
        _mockPriceCalculator = new Mock<IPriceCalculatorService>();
        _service = new PrintOrderService(_unitOfWork, _mockPriceCalculator.Object);

        // Seed Users
        var student = new User
        {
            Id = _studentId,
            FullName = "Tran Huynh Khoi",
            Email = "khoith@uniprint.edu.vn",
            PasswordHash = "hashed_pw",
            Role = UserRole.Student
        };
        var otherStudent = new User
        {
            Id = _otherStudentId,
            FullName = "Le Van Other",
            Email = "other@uniprint.edu.vn",
            PasswordHash = "hashed_pw",
            Role = UserRole.Student
        };
        _dbContext.Users.AddRange(student, otherStudent);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task CreateOrderAsync_ValidRequest_ShouldCreateOrder_WithOrdPrefixAndLockedPrice_BR02()
    {
        // Arrange
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _studentId,
            FileName = "BaoCaoKhoaHoc.pdf",
            PageCount = 10,
            FileSizeBytes = 2048,
            FileExtension = ".pdf"
        };
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.SaveChangesAsync();

        var expectedPrice = 4500m;
        var unitPrice = 315m;
        _mockPriceCalculator.Setup(p => p.CalculatePriceAsync(It.IsAny<CalculatePriceRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<PriceCalculationResultDto>.Ok(new PriceCalculationResultDto
            {
                UnitPricePerPage = unitPrice,
                PageSubtotal = 3150m,
                ExtraServicesTotal = 1350m,
                TotalEstimatedPrice = expectedPrice
            }));

        var request = new CreatePrintOrderDto
        {
            DocumentId = doc.Id,
            NumberOfCopies = 1,
            IsColor = false,
            IsDoubleSided = true,
            HasBinding = false,
            HasStaple = true,
            PaymentMethod = PaymentMethod.Cash,
            Notes = "In gấp cho tôi trước 11h"
        };

        // Act
        var result = await _service.CreateOrderAsync(_studentId, request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();

        var orderDto = result.Data!;
        // Mã đơn phải bắt đầu bằng ORD-YYYYMMDD-
        orderDto.OrderCode.Should().StartWith("ORD-");
        orderDto.DocumentName.Should().Be("BaoCaoKhoaHoc.pdf");
        orderDto.StudentName.Should().Be("Tran Huynh Khoi");
        orderDto.Status.Should().Be(PrintOrderStatus.Pending);

        // BR02: Giá chốt cố định theo thời điểm tạo đơn
        orderDto.TotalPrice.Should().Be(expectedPrice);

        // QR Token được cấp với hạn 7 ngày
        orderDto.QrToken.Should().NotBeNullOrEmpty();
        orderDto.QrExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(6));

        // Kiểm tra database
        var dbOrder = await _dbContext.PrintOrders
            .Include(o => o.PrintOption)
            .Include(o => o.PickupQR)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == orderDto.Id);

        dbOrder.Should().NotBeNull();
        dbOrder!.OrderCode.Should().Be(orderDto.OrderCode);
        dbOrder.Notes.Should().Be("In gấp cho tôi trước 11h");
        dbOrder.PrintOption.Should().NotBeNull();
        dbOrder.PrintOption.TotalPrice.Should().Be(expectedPrice);
        dbOrder.PrintOption.UnitPricePerPage.Should().Be(unitPrice);
        dbOrder.PickupQR.Should().NotBeNull();
        dbOrder.PickupQR.IsUsed.Should().BeFalse();
        dbOrder.Payment.Should().NotBeNull();
        dbOrder.Payment.Amount.Should().Be(expectedPrice);
        dbOrder.Payment.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task CreateOrderAsync_ZeroOrNegativeCopies_ShouldReturnFail()
    {
        // Arrange
        var request = new CreatePrintOrderDto
        {
            DocumentId = Guid.NewGuid(),
            NumberOfCopies = 0
        };

        // Act
        var result = await _service.CreateOrderAsync(_studentId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Số bản in");
    }

    [Fact]
    public async Task CreateOrderAsync_NonExistentDocument_ShouldReturnFail()
    {
        // Arrange
        var request = new CreatePrintOrderDto
        {
            DocumentId = Guid.NewGuid(),
            NumberOfCopies = 1
        };

        // Act
        var result = await _service.CreateOrderAsync(_studentId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không tìm thấy");
    }

    [Fact]
    public async Task CreateOrderAsync_DocumentNotOwnedByStudent_ShouldReturnFail()
    {
        // Arrange: Document của sinh viên khác
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _otherStudentId,
            FileName = "TaiLieuRiengTu.pdf",
            PageCount = 5,
            FileSizeBytes = 1024,
            FileExtension = ".pdf"
        };
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.SaveChangesAsync();

        var request = new CreatePrintOrderDto
        {
            DocumentId = doc.Id,
            NumberOfCopies = 1
        };

        // Act
        var result = await _service.CreateOrderAsync(_studentId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("quyền in");
    }

    [Fact]
    public async Task CreateOrderAsync_ApprovedStudyMaterialDocument_ShouldSucceed()
    {
        // Arrange: Document thuộc StudyMaterial đã được duyệt bởi Admin
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = _otherStudentId,
            FileName = "GiaoTrinhChung.pdf",
            PageCount = 20,
            FileSizeBytes = 4096,
            FileExtension = ".pdf"
        };
        var studyMaterial = new StudyMaterial
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            Title = "Giáo trình PRN232",
            UploadedByStudentId = _otherStudentId,
            Status = MaterialStatus.Approved
        };
        await _dbContext.Documents.AddAsync(doc);
        await _dbContext.StudyMaterials.AddAsync(studyMaterial);
        await _dbContext.SaveChangesAsync();

        _mockPriceCalculator.Setup(p => p.CalculatePriceAsync(It.IsAny<CalculatePriceRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<PriceCalculationResultDto>.Ok(new PriceCalculationResultDto
            {
                UnitPricePerPage = 350m,
                PageSubtotal = 7000m,
                ExtraServicesTotal = 0,
                TotalEstimatedPrice = 7000m
            }));

        var request = new CreatePrintOrderDto
        {
            DocumentId = doc.Id,
            NumberOfCopies = 1
        };

        // Act
        var result = await _service.CreateOrderAsync(_studentId, request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalPrice.Should().Be(7000m);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
