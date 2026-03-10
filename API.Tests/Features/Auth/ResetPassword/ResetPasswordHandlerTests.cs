using API.Features.Auth.ResetPassword;
using API.Infrastructure.Data;
using API.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Common.Errors;
using Shared.Contracts.Auth;
using Xunit;

namespace API.Tests.Features.Auth.ResetPassword;

/// <summary>
/// Unit tests for ResetPasswordHandler.
/// Tests password reset logic including token validation and password update.
/// </summary>
public class ResetPasswordHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<User>> _mockUserManager;
    private readonly Mock<ILogger<ResetPasswordHandler>> _mockLogger;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly ResetPasswordHandler _handler;

    public ResetPasswordHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _mockUserManager = MockUserManager<User>();
        _mockLogger = new Mock<ILogger<ResetPasswordHandler>>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        // Setup HttpContext
        var httpContext = new DefaultHttpContext();
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        _handler = new ResetPasswordHandler(
            _mockUserManager.Object,
            _mockLogger.Object,
            _mockHttpContextAccessor.Object
        );
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Token_Invalid()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Token = "invalid-token",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        _mockUserManager.Setup(x => x.Users)
            .Returns(_context.Users);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.PasswordResetTokenInvalid);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Token_Expired()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            PasswordResetToken = "expired-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(-1) // Expired 1 hour ago
        };

        var request = new ResetPasswordRequest
        {
            Token = "expired-token",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.PasswordResetTokenExpired);

        // Token should be cleared
        user.PasswordResetToken.Should().BeNull();
        user.PasswordResetTokenExpiry.Should().BeNull();

        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Token_Expiry_Is_Null()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            PasswordResetToken = "token-no-expiry",
            PasswordResetTokenExpiry = null // No expiry set
        };

        var request = new ResetPasswordRequest
        {
            Token = "token-no-expiry",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.PasswordResetTokenExpired);
    }

    [Fact]
    public async Task Handle_Should_Reset_Password_With_Valid_Token()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1)
        };

        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.RemovePasswordAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.AddPasswordAsync(user, request.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Contain("successfully");

        _mockUserManager.Verify(x => x.RemovePasswordAsync(user), Times.Once);
        _mockUserManager.Verify(x => x.AddPasswordAsync(user, request.NewPassword), Times.Once);
        _mockUserManager.Verify(x => x.UpdateSecurityStampAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Clear_Reset_Token_After_Success()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1)
        };

        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.RemovePasswordAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.AddPasswordAsync(user, request.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Token should be cleared
        user.PasswordResetToken.Should().BeNull();
        user.PasswordResetTokenExpiry.Should().BeNull();

        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Update_Security_Stamp_To_Invalidate_Tokens()
    {
        // Arrange - Security stamp update invalidates existing auth tokens
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1)
        };

        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.RemovePasswordAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.AddPasswordAsync(user, request.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockUserManager.Verify(x => x.UpdateSecurityStampAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Remove_Password_Fails()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1)
        };

        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        var identityError = new IdentityError { Code = "RemoveError", Description = "Failed to remove password" };
        _mockUserManager.Setup(x => x.RemovePasswordAsync(user))
            .ReturnsAsync(IdentityResult.Failed(identityError));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("PASSWORD_UPDATE_FAILED");

        _mockUserManager.Verify(x => x.AddPasswordAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Add_Password_Fails()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1)
        };

        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "weak", // Weak password
            ConfirmPassword = "weak"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.RemovePasswordAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var identityError = new IdentityError { Code = "PasswordTooWeak", Description = "Password is too weak" };
        _mockUserManager.Setup(x => x.AddPasswordAsync(user, request.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(identityError));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("PASSWORD_UPDATE_FAILED");
        result.Error.Message.Should().Contain("too weak");

        _mockUserManager.Verify(x => x.UpdateSecurityStampAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Continue_Even_If_Token_Clear_Fails()
    {
        // Arrange - Even if clearing token fails, password was reset successfully
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1)
        };

        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.RemovePasswordAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.AddPasswordAsync(user, request.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        var identityError = new IdentityError { Code = "UpdateError", Description = "Database error" };
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(identityError));

        _mockUserManager.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(request);

        // Assert - Should still return success
        result.IsSuccess.Should().BeTrue();
    }

    // Helper method
    private static Mock<UserManager<User>> MockUserManager<TUser>() where TUser : class
    {
        var store = new Mock<IUserStore<TUser>>();
        var mock = new Mock<UserManager<TUser>>(
            store.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);
        return mock as Mock<UserManager<User>> ?? new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object,
            null!, null!, null!, null!, null!, null!, null!, null!);
    }

    public void Dispose()
    {
        _context?.Database.EnsureDeleted();
        _context?.Dispose();
    }
}
