using API.Features.Auth.VerifyEmail;
using API.Infrastructure.Data;
using API.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Common.Errors;
using Shared.Contracts.Auth;
using Xunit;

namespace API.Tests.Features.Auth.VerifyEmail;

/// <summary>
/// Unit tests for VerifyEmailHandler.
/// Tests email verification logic including token validation and expiry.
/// </summary>
public class VerifyEmailHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<User>> _mockUserManager;
    private readonly Mock<ILogger<VerifyEmailHandler>> _mockLogger;
    private readonly VerifyEmailHandler _handler;

    public VerifyEmailHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _mockUserManager = MockUserManager<User>();
        _mockLogger = new Mock<ILogger<VerifyEmailHandler>>();

        _handler = new VerifyEmailHandler(
            _mockUserManager.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Token_Invalid()
    {
        // Arrange
        var request = new VerifyEmailRequest(Token: "invalid-token");

        _mockUserManager.Setup(x => x.Users)
            .Returns(_context.Users);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EmailErrors.InvalidToken);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_User_Not_Found_By_UserId()
    {
        // Arrange
        var request = new VerifyEmailRequest(Token: "valid-token", UserId: "999");

        _mockUserManager.Setup(x => x.FindByIdAsync("999"))
            .ReturnsAsync((User?)null);

        _mockUserManager.Setup(x => x.Users)
            .Returns(_context.Users);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EmailErrors.InvalidToken);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Token_Does_Not_Match_UserId()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            EmailVerificationToken = "correct-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            EmailConfirmed = false
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new VerifyEmailRequest(Token: "wrong-token", UserId: "1");

        _mockUserManager.Setup(x => x.FindByIdAsync("1"))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EmailErrors.InvalidToken);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Email_Already_Verified()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            EmailConfirmed = true // Already verified
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new VerifyEmailRequest(Token: "valid-token");

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EmailErrors.AlreadyVerified);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Token_Expired()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            EmailVerificationToken = "expired-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(-1), // Expired 1 hour ago
            EmailConfirmed = false
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new VerifyEmailRequest(Token: "expired-token");

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EmailErrors.InvalidToken);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Token_Expiry_Is_Null()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            EmailVerificationToken = "token-no-expiry",
            EmailVerificationTokenExpiry = null, // No expiry set
            EmailConfirmed = false
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new VerifyEmailRequest(Token: "token-no-expiry");

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EmailErrors.InvalidToken);
    }

    [Fact]
    public async Task HandleAsync_Should_Verify_Email_With_Valid_Token()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            EmailConfirmed = false
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new VerifyEmailRequest(Token: "valid-token");

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Message.Should().NotBeNullOrEmpty();
        result.Value.UserId.Should().Be("1");
        result.Value.Email.Should().Be("test@test.com");

        // Verify email was confirmed
        user.EmailConfirmed.Should().BeTrue();

        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_Verify_Email_When_UserId_Provided()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            EmailConfirmed = false
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new VerifyEmailRequest(Token: "valid-token", UserId: "1");

        _mockUserManager.Setup(x => x.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.UserId.Should().Be("1");
        result.Value.Email.Should().Be("test@test.com");

        user.EmailConfirmed.Should().BeTrue();

        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_Keep_Token_For_CompleteRegistration()
    {
        // Arrange - Token should remain for use in CompleteRegistration endpoint
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            EmailConfirmed = false
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new VerifyEmailRequest(Token: "valid-token");

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Token should still be present (not cleared)
        user.EmailVerificationToken.Should().Be("valid-token");
        user.EmailVerificationTokenExpiry.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Update_Fails()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            EmailConfirmed = false
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new VerifyEmailRequest(Token: "valid-token");

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        var identityError = new IdentityError { Code = "UpdateError", Description = "Database error" };
        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Failed(identityError));

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.UpdateFailed);

        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_Find_User_By_Token_When_UserId_Not_Provided()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            EmailVerificationToken = "find-by-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            EmailConfirmed = false
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Request without UserId - should find by token
        var request = new VerifyEmailRequest(Token: "find-by-token");

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be("1");
        result.Value.Email.Should().Be("test@test.com");

        user.EmailConfirmed.Should().BeTrue();
    }

    public void Dispose()
    {
        _context?.Database.EnsureDeleted();
        _context?.Dispose();
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
}
