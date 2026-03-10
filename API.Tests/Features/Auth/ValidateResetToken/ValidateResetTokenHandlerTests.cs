using API.Features.Auth.ValidateResetToken;
using API.Infrastructure.Data;
using API.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Common.Constants;
using Shared.Contracts.Auth;
using Xunit;

namespace API.Tests.Features.Auth.ValidateResetToken;

/// <summary>
/// Unit tests for ValidateResetTokenHandler.
/// Tests reset token validation logic.
/// </summary>
public class ValidateResetTokenHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<User>> _mockUserManager;
    private readonly Mock<ILogger<ValidateResetTokenHandler>> _mockLogger;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly ValidateResetTokenHandler _handler;

    public ValidateResetTokenHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _mockUserManager = MockUserManager<User>();
        _mockLogger = new Mock<ILogger<ValidateResetTokenHandler>>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        // Setup HttpContext
        var httpContext = new DefaultHttpContext();
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        _handler = new ValidateResetTokenHandler(
            _mockUserManager.Object,
            _mockLogger.Object,
            _mockHttpContextAccessor.Object
        );
    }

    [Fact]
    public async Task Handle_Should_Return_Invalid_When_Token_Not_Found()
    {
        // Arrange - Security: Return generic error to prevent enumeration
        var request = new ValidateResetTokenRequest
        {
            Token = "non-existent-token"
        };

        _mockUserManager.Setup(x => x.Users)
            .Returns(_context.Users);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue(); // Always returns success for security
        result.Value.IsValid.Should().BeFalse();
        result.Value.ErrorCode.Should().Be(ErrorCodes.UserPasswordResetTokenInvalid);
    }

    [Fact]
    public async Task Handle_Should_Return_Invalid_When_Token_Expired()
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

        var request = new ValidateResetTokenRequest
        {
            Token = "expired-token"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue(); // Always returns success for security
        result.Value.IsValid.Should().BeFalse();
        result.Value.ErrorCode.Should().Be(ErrorCodes.UserPasswordResetTokenInvalid);

        // Token should be cleared
        user.PasswordResetToken.Should().BeNull();
        user.PasswordResetTokenExpiry.Should().BeNull();

        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Invalid_When_Token_Expiry_Is_Null()
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

        var request = new ValidateResetTokenRequest
        {
            Token = "token-no-expiry"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeFalse();
        result.Value.ErrorCode.Should().Be(ErrorCodes.UserPasswordResetTokenInvalid);
    }

    [Fact]
    public async Task Handle_Should_Return_Valid_For_Valid_Token()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            PasswordResetToken = "valid-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1) // Expires in 1 hour
        };

        var request = new ValidateResetTokenRequest
        {
            Token = "valid-token"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeTrue();
        result.Value.ErrorCode.Should().BeNull();

        // Token should NOT be cleared for valid tokens
        user.PasswordResetToken.Should().Be("valid-token");
        user.PasswordResetTokenExpiry.Should().NotBeNull();

        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Valid_When_Token_Just_Created()
    {
        // Arrange - Token created just now
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            PasswordResetToken = "fresh-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1)
        };

        var request = new ValidateResetTokenRequest
        {
            Token = "fresh-token"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeTrue();
        result.Value.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Return_Valid_When_Token_About_To_Expire()
    {
        // Arrange - Token expires in 1 minute
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            PasswordResetToken = "almost-expired-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(1)
        };

        var request = new ValidateResetTokenRequest
        {
            Token = "almost-expired-token"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeTrue();
        result.Value.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Clear_Expired_Token_From_Database()
    {
        // Arrange - Expired token should be cleaned up
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            PasswordResetToken = "expired-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(-1) // Expired 1 minute ago
        };

        var request = new ValidateResetTokenRequest
        {
            Token = "expired-token"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.Value.IsValid.Should().BeFalse();

        // Verify token was cleared
        user.PasswordResetToken.Should().BeNull();
        user.PasswordResetTokenExpiry.Should().BeNull();

        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Always_Return_Success_For_Security()
    {
        // Arrange - Even for invalid tokens, HTTP status should be 200
        // This prevents enumeration attacks

        var request1 = new ValidateResetTokenRequest { Token = "invalid" };
        var request2 = new ValidateResetTokenRequest { Token = "expired" };

        _mockUserManager.Setup(x => x.Users)
            .Returns(_context.Users);

        // Act
        var result1 = await _handler.Handle(request1);
        var result2 = await _handler.Handle(request2);

        // Assert - Both should return success (200 OK) but with IsValid = false
        result1.IsSuccess.Should().BeTrue();
        result1.Value.IsValid.Should().BeFalse();

        result2.IsSuccess.Should().BeTrue();
        result2.Value.IsValid.Should().BeFalse();
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
