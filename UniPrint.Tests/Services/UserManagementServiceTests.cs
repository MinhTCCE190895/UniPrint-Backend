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

/// <summary>
/// Task 5: Unit Tests kiểm tra nghiệp vụ Quản lý người dùng của Admin:
/// - Lấy danh sách, tìm kiếm, lọc theo vai trò và trạng thái
/// - Phân quyền người dùng (chặn tự đổi quyền)
/// - Khóa / Mở khóa tài khoản (chặn tự khóa tài khoản)
/// - Kiểm tra tài khoản bị khóa không thể đăng nhập
/// </summary>
public class UserManagementServiceTests : IDisposable
{
    private readonly UniPrintDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserService _userService;
    private readonly AuthService _authService;

    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();

    public UserManagementServiceTests()
    {
        var options = new DbContextOptionsBuilder<UniPrintDbContext>()
            .UseInMemoryDatabase(databaseName: $"UniPrint_UserMgmt_Test_{Guid.NewGuid()}")
            .Options;

        _dbContext = new UniPrintDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);
        _userService = new UserService(_unitOfWork);

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["JwtSettings:Secret"]).Returns("A_Very_Long_Secret_Key_For_Testing_Purposes_Must_Be_32_Chars_Long!");
        mockConfig.Setup(c => c["JwtSettings:Issuer"]).Returns("UniPrint_API");
        mockConfig.Setup(c => c["JwtSettings:Audience"]).Returns("UniPrint_Client");
        _authService = new AuthService(_unitOfWork, mockConfig.Object);

        SeedTestUsers();
    }

    private void SeedTestUsers()
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("123456");

        _dbContext.Users.AddRange(
            new User
            {
                Id = _adminId,
                FullName = "Admin Principal",
                Email = "admin@uniprint.edu.vn",
                PhoneNumber = "0901111222",
                PasswordHash = passwordHash,
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new User
            {
                Id = _staffId,
                FullName = "Staff Member",
                Email = "staff@uniprint.edu.vn",
                PhoneNumber = "0903333444",
                PasswordHash = passwordHash,
                Role = UserRole.Staff,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new User
            {
                Id = _studentId,
                FullName = "Student User",
                Email = "student@uniprint.edu.vn",
                PhoneNumber = "0905555666",
                PasswordHash = passwordHash,
                Role = UserRole.Student,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        );

        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetUsersAsync_ShouldReturnAllUsers_OrderedByCreatedAtDescending()
    {
        // Act
        var result = await _userService.GetUsersAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Count.Should().Be(3);
        result.Data![0].Id.Should().Be(_studentId); // Created most recently
        result.Data![2].Id.Should().Be(_adminId);   // Created earliest
    }

    [Theory]
    [InlineData("Student", 1)]
    [InlineData("staff@uniprint.edu.vn", 1)]
    [InlineData("0901111222", 1)]
    [InlineData("nonexistent", 0)]
    public async Task GetUsersAsync_WithSearchTerm_ShouldFilterCorrectly(string searchTerm, int expectedCount)
    {
        // Act
        var result = await _userService.GetUsersAsync(searchTerm: searchTerm);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Count.Should().Be(expectedCount);
    }

    [Fact]
    public async Task GetUsersAsync_WithRoleFilter_ShouldFilterCorrectly()
    {
        // Act
        var result = await _userService.GetUsersAsync(role: UserRole.Staff);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Should().ContainSingle();
        result.Data![0].Role.Should().Be(UserRole.Staff);
        result.Data![0].FullName.Should().Be("Staff Member");
    }

    [Fact]
    public async Task GetUsersAsync_WithStatusFilter_ShouldFilterCorrectly()
    {
        // Arrange: Lock the student
        var student = await _dbContext.Users.FindAsync(_studentId);
        student!.IsActive = false;
        await _dbContext.SaveChangesAsync();

        // Act
        var activeUsers = await _userService.GetUsersAsync(isActive: true);
        var lockedUsers = await _userService.GetUsersAsync(isActive: false);

        // Assert
        activeUsers.Data!.Count.Should().Be(2);
        lockedUsers.Data!.Count.Should().Be(1);
        lockedUsers.Data![0].Id.Should().Be(_studentId);
    }

    [Fact]
    public async Task GetUserByIdAsync_ShouldReturnUser_WhenExists()
    {
        // Act
        var result = await _userService.GetUserByIdAsync(_studentId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.FullName.Should().Be("Student User");
        result.Data.Role.Should().Be(UserRole.Student);
    }

    [Fact]
    public async Task GetUserByIdAsync_ShouldFail_WhenNotFound()
    {
        // Act
        var result = await _userService.GetUserByIdAsync(Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không tìm thấy");
    }

    [Fact]
    public async Task UpdateUserRoleAsync_ShouldSucceed_WhenValid()
    {
        // Act: Promote student to staff
        var result = await _userService.UpdateUserRoleAsync(_adminId, _studentId, new UpdateUserRoleDto
        {
            Role = UserRole.Staff
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Role.Should().Be(UserRole.Staff);
        result.Data.RoleName.Should().Be("Nhân viên");

        // Verify in DB
        var userInDb = await _dbContext.Users.FindAsync(_studentId);
        userInDb!.Role.Should().Be(UserRole.Staff);
    }

    [Fact]
    public async Task UpdateUserRoleAsync_ShouldFail_WhenAdminChangesOwnRole()
    {
        // Act
        var result = await _userService.UpdateUserRoleAsync(_adminId, _adminId, new UpdateUserRoleDto
        {
            Role = UserRole.Student
        });

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không thể tự thay đổi vai trò");

        // Verify role unchanged
        var adminInDb = await _dbContext.Users.FindAsync(_adminId);
        adminInDb!.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task UpdateUserRoleAsync_ShouldFail_WhenTargetUserNotFound()
    {
        // Act
        var result = await _userService.UpdateUserRoleAsync(_adminId, Guid.NewGuid(), new UpdateUserRoleDto
        {
            Role = UserRole.Staff
        });

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không tìm thấy");
    }

    [Fact]
    public async Task ToggleUserStatusAsync_ShouldLockUser_WhenCurrentlyActive()
    {
        // Act: Lock student
        var result = await _userService.ToggleUserStatusAsync(_adminId, _studentId, new ToggleUserStatusDto
        {
            IsActive = false
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.IsActive.Should().BeFalse();
        result.Message.Should().Contain("Khóa tài khoản thành công");

        var studentInDb = await _dbContext.Users.FindAsync(_studentId);
        studentInDb!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleUserStatusAsync_ShouldUnlockUser_WhenCurrentlyLocked()
    {
        // Arrange
        var student = await _dbContext.Users.FindAsync(_studentId);
        student!.IsActive = false;
        await _dbContext.SaveChangesAsync();

        // Act: Unlock
        var result = await _userService.ToggleUserStatusAsync(_adminId, _studentId, new ToggleUserStatusDto
        {
            IsActive = true
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.IsActive.Should().BeTrue();
        result.Message.Should().Contain("Mở khóa tài khoản thành công");

        var studentInDb = await _dbContext.Users.FindAsync(_studentId);
        studentInDb!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleUserStatusAsync_ShouldFail_WhenAdminLocksSelf()
    {
        // Act
        var result = await _userService.ToggleUserStatusAsync(_adminId, _adminId, new ToggleUserStatusDto
        {
            IsActive = false
        });

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không thể tự khóa");

        var adminInDb = await _dbContext.Users.FindAsync(_adminId);
        adminInDb!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleUserStatusAsync_ShouldFail_WhenTargetNotFound()
    {
        // Act
        var result = await _userService.ToggleUserStatusAsync(_adminId, Guid.NewGuid());

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Không tìm thấy");
    }

    [Fact]
    public async Task AuthService_LoginAsync_ShouldFail_WhenUserIsLocked()
    {
        // Arrange: Lock student
        await _userService.ToggleUserStatusAsync(_adminId, _studentId, new ToggleUserStatusDto { IsActive = false });

        // Act: Attempt to login
        var loginResult = await _authService.LoginAsync(new LoginRequestDto
        {
            Email = "student@uniprint.edu.vn",
            Password = "123456"
        });

        // Assert
        loginResult.Success.Should().BeFalse();
        loginResult.Message.Should().Contain("Tài khoản của bạn đã bị khóa");
    }

    [Fact]
    public async Task UpdateUserRoleAsync_ShouldFail_WhenPromotingToAdmin()
    {
        // Act
        var result = await _userService.UpdateUserRoleAsync(_adminId, _staffId, new UpdateUserRoleDto
        {
            Role = UserRole.Admin
        });

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Admin");

        var staffInDb = await _dbContext.Users.FindAsync(_staffId);
        staffInDb!.Role.Should().Be(UserRole.Staff);
    }

    [Fact]
    public async Task UpdateUserRoleAsync_ShouldFail_WhenChangingOtherAdminRole()
    {
        // Arrange: Seed another Admin
        var otherAdminId = Guid.NewGuid();
        _dbContext.Users.Add(new User
        {
            Id = otherAdminId,
            FullName = "Second Admin",
            Email = "admin2@uniprint.edu.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Role = UserRole.Admin,
            IsActive = true
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _userService.UpdateUserRoleAsync(_adminId, otherAdminId, new UpdateUserRoleDto
        {
            Role = UserRole.Staff
        });

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Quản trị viên");

        var adminInDb = await _dbContext.Users.FindAsync(otherAdminId);
        adminInDb!.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task CreateStaffAsync_ShouldSucceed_WhenValid()
    {
        // Act
        var result = await _userService.CreateStaffAsync(new CreateStaffRequestDto
        {
            FullName = "Nhan Vien Quầy In",
            Email = "Staff.New@uniprint.edu.vn",
            Password = "StaffPassword123",
            PhoneNumber = "0912345678"
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Role.Should().Be(UserRole.Staff);
        result.Data.Email.Should().Be("staff.new@uniprint.edu.vn");
        result.Data.IsActive.Should().BeTrue();

        var createdInDb = await _dbContext.Users.FindAsync(result.Data.Id);
        createdInDb.Should().NotBeNull();
        BCrypt.Net.BCrypt.Verify("StaffPassword123", createdInDb!.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task CreateStaffAsync_ShouldFail_WhenEmailAlreadyExists()
    {
        // Act
        var result = await _userService.CreateStaffAsync(new CreateStaffRequestDto
        {
            FullName = "Duplicate Staff",
            Email = "STUDENT@uniprint.edu.vn",
            Password = "StaffPassword123"
        });

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Email đã được đăng ký");
    }

    [Fact]
    public async Task CreateStaffAsync_ShouldFail_WhenPasswordTooShort()
    {
        // Act
        var result = await _userService.CreateStaffAsync(new CreateStaffRequestDto
        {
            FullName = "Short Password Staff",
            Email = "shortpwd@uniprint.edu.vn",
            Password = "123"
        });

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("ít nhất 6 ký tự");
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldSucceed_WhenValid()
    {
        // Act
        var result = await _userService.ResetPasswordAsync(_adminId, _studentId, new AdminResetPasswordDto
        {
            NewPassword = "ResetPass@2026"
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(_studentId);

        var studentInDb = await _dbContext.Users.FindAsync(_studentId);
        BCrypt.Net.BCrypt.Verify("ResetPass@2026", studentInDb!.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldFail_WhenResettingSelfOrOtherAdmin()
    {
        // Self reset
        var selfResult = await _userService.ResetPasswordAsync(_adminId, _adminId, new AdminResetPasswordDto
        {
            NewPassword = "ResetPass@2026"
        });
        selfResult.Success.Should().BeFalse();

        // Other Admin reset
        var otherAdminId = Guid.NewGuid();
        _dbContext.Users.Add(new User
        {
            Id = otherAdminId,
            FullName = "Second Admin",
            Email = "admin2@uniprint.edu.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
            Role = UserRole.Admin,
            IsActive = true
        });
        await _dbContext.SaveChangesAsync();

        var otherAdminResult = await _userService.ResetPasswordAsync(_adminId, otherAdminId, new AdminResetPasswordDto
        {
            NewPassword = "ResetPass@2026"
        });
        otherAdminResult.Success.Should().BeFalse();
        otherAdminResult.Message.Should().Contain("Quản trị viên");
    }
}

