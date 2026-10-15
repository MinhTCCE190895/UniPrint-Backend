using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using UniPrint.Business.DTOs;
using UniPrint.Business.Services;
using UniPrint.DataAccess.Context;
using UniPrint.DataAccess.Entities;
using UniPrint.DataAccess.Enums;
using UniPrint.DataAccess.Repositories;
using Xunit;

namespace UniPrint.Tests.Services;

public class UserProfileTests : IDisposable
{
    private readonly UniPrintDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly AuthService _authService;
    private readonly Guid _userId = Guid.NewGuid();

    public UserProfileTests()
    {
        var options = new DbContextOptionsBuilder<UniPrintDbContext>()
            .UseInMemoryDatabase(databaseName: $"UniPrint_Profile_Test_{Guid.NewGuid()}")
            .Options;

        _dbContext = new UniPrintDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);
        _mockConfig = new Mock<IConfiguration>();
        _mockConfig.Setup(c => c["JwtSettings:Secret"]).Returns("A_Very_Long_Secret_Key_For_Testing_Purposes_Must_Be_32_Chars_Long!");
        _mockConfig.Setup(c => c["JwtSettings:Issuer"]).Returns("UniPrint_API");
        _mockConfig.Setup(c => c["JwtSettings:Audience"]).Returns("UniPrint_Client");

        _authService = new AuthService(_unitOfWork, _mockConfig.Object);

        // Seed test user with known password
        var user = new User
        {
            Id = _userId,
            FullName = "Tran Cong Minh",
            Email = "minhtc@uniprint.edu.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123"),
            PhoneNumber = "0901234567",
            Role = UserRole.Student,
            IsActive = true
        };
        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetProfileAsync_ShouldReturnProfile_WhenUserExists()
    {
        // Act
        var result = await _authService.GetProfileAsync(_userId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.FullName.Should().Be("Tran Cong Minh");
        result.Data.Email.Should().Be("minhtc@uniprint.edu.vn");
        result.Data.PhoneNumber.Should().Be("0901234567");
    }

    [Fact]
    public async Task UpdateProfileAsync_ShouldUpdatePhoneNumberAndFullName_WhenValid()
    {
        // Arrange
        var request = new UpdateProfileRequestDto
        {
            FullName = "Tran Cong Minh Updated",
            PhoneNumber = "0987654321"
        };

        // Act
        var result = await _authService.UpdateProfileAsync(_userId, request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.FullName.Should().Be("Tran Cong Minh Updated");
        result.Data.PhoneNumber.Should().Be("0987654321");

        var updatedInDb = await _dbContext.Users.FindAsync(_userId);
        updatedInDb!.FullName.Should().Be("Tran Cong Minh Updated");
        updatedInDb.PhoneNumber.Should().Be("0987654321");
    }

    [Fact]
    public async Task UpdateProfileAsync_ShouldFail_WhenPhoneNumberIsInvalid()
    {
        // Arrange
        var request = new UpdateProfileRequestDto
        {
            FullName = "Tran Cong Minh",
            PhoneNumber = "12345" // Invalid phone number
        };

        // Act
        var result = await _authService.UpdateProfileAsync(_userId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Số điện thoại không hợp lệ");
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldSucceed_WhenCredentialsAreValid()
    {
        // Arrange
        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "OldPassword123",
            NewPassword = "NewPassword456",
            ConfirmPassword = "NewPassword456"
        };

        // Act
        var result = await _authService.ChangePasswordAsync(_userId, request);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Đổi mật khẩu thành công");

        var updatedInDb = await _dbContext.Users.FindAsync(_userId);
        BCrypt.Net.BCrypt.Verify("NewPassword456", updatedInDb!.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldFail_WhenCurrentPasswordIsWrong()
    {
        // Arrange
        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "WrongPassword",
            NewPassword = "NewPassword456",
            ConfirmPassword = "NewPassword456"
        };

        // Act
        var result = await _authService.ChangePasswordAsync(_userId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Mật khẩu hiện tại không chính xác");
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldFail_WhenConfirmPasswordDoesNotMatch()
    {
        // Arrange
        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = "OldPassword123",
            NewPassword = "NewPassword456",
            ConfirmPassword = "DifferentPassword"
        };

        // Act
        var result = await _authService.ChangePasswordAsync(_userId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("không khớp");
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
