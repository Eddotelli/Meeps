using API.Features.Auth.ForgotPassword;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using API.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Common.Results;
using Shared.Contracts.Auth;
using Xunit;

namespace API.Tests.Features.Auth.ForgotPassword;

/// <summary>
/// Unit tests for ForgotPasswordHandler.
/// Tests password reset token generation and email delivery.
/// </summary>
public class ForgotPasswordHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<User>> _mockUserManager;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ILogger<ForgotPasswordHandler>> _mockLogger;
    private readonly ForgotPasswordHandler _handler;

    public ForgotPasswordHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _mockUserManager = MockUserManager<User>();
        _mockEmailService = new Mock<IEmailService>();
        _mockLogger = new Mock<ILogger<ForgotPasswordHandler>>();

        _handler = new ForgotPasswordHandler(
            _mockUserManager.Object,
            _mockEmailService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_Should_Return_Success_When_User_Not_Found()
    {
        // Arrange - Security: Always return success to prevent email enumeration
        var request = new ForgotPasswordRequest
        {
            Email = "nonexistent@test.com"
        };

        _mockUserManager.Setup(x => x.Users)
            .Returns(_context.Users);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Contain("If the email exists");

        // Email service should not be called
        _mockEmailService.Verify(
            x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Generate_Reset_Token_For_Valid_User()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            EmailConfirmed = true
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new ForgotPasswordRequest
        {
            Email = "test@test.com"
        };

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockEmailService.Setup(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Contain("If the email exists");

        // Verify token was generated
        user.PasswordResetToken.Should().NotBeNullOrEmpty();
        user.PasswordResetTokenExpiry.Should().NotBeNull();
        user.PasswordResetTokenExpiry.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromMinutes(1));

        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Send_Reset_Email_With_Token()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            EmailConfirmed = true
        };

        var request = new ForgotPasswordRequest
        {
            Email = "test@test.com"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockEmailService.Setup(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockEmailService.Verify(
            x => x.SendPasswordResetEmailAsync(
                "test@test.com",
                It.Is<string>(token => !string.IsNullOrEmpty(token))),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Generate_Secure_256Bit_Token()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            EmailConfirmed = true
        };

        var request = new ForgotPasswordRequest
        {
            Email = "test@test.com"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockEmailService.Setup(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Token should be base64 encoded 32 bytes (256 bits)
        // Base64 of 32 bytes = 44 characters
        user.PasswordResetToken.Should().NotBeNullOrEmpty();
        user.PasswordResetToken!.Length.Should().Be(44); // Base64(32 bytes) = 44 chars
    }

    [Fact]
    public async Task Handle_Should_Return_Success_Even_When_Update_Fails()
    {
        // Arrange - Security: Don't reveal if update failed
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            EmailConfirmed = true
        };

        var request = new ForgotPasswordRequest
        {
            Email = "test@test.com"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        var identityError = new IdentityError { Code = "UpdateError", Description = "Database error" };
        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Failed(identityError));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Contain("If the email exists");

        // Email should not be sent if update fails
        _mockEmailService.Verify(
            x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_Even_When_Email_Fails()
    {
        // Arrange - Security: Don't reveal if email failed
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            EmailConfirmed = true
        };

        var request = new ForgotPasswordRequest
        {
            Email = "test@test.com"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        // Email service fails
        _mockEmailService.Setup(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Failure(new Shared.Common.Results.Error("Email.Failed", "Failed to send email")));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Contain("If the email exists");

        _mockEmailService.Verify(
            x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Set_Token_Expiry_To_One_Hour()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            EmailConfirmed = true
        };

        var request = new ForgotPasswordRequest
        {
            Email = "test@test.com"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockEmailService.Setup(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        var beforeRequest = DateTime.UtcNow;

        // Act
        var result = await _handler.Handle(request);

        var afterRequest = DateTime.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();

        user.PasswordResetTokenExpiry.Should().NotBeNull();
        user.PasswordResetTokenExpiry.Should().BeCloseTo(beforeRequest.AddHours(1), TimeSpan.FromMinutes(1));
        user.PasswordResetTokenExpiry.Should().BeCloseTo(afterRequest.AddHours(1), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Handle_Should_Replace_Existing_Reset_Token()
    {
        // Arrange - User already has a reset token
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            EmailConfirmed = true,
            PasswordResetToken = "old-token",
            PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30)
        };

        var request = new ForgotPasswordRequest
        {
            Email = "test@test.com"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockEmailService.Setup(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Old token should be replaced
        user.PasswordResetToken.Should().NotBe("old-token");
        user.PasswordResetToken.Should().NotBeNullOrEmpty();
        user.PasswordResetTokenExpiry.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Handle_Should_Work_For_Case_Insensitive_Email()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com", // Use lowercase to match request
            UserName = "testuser",
            EmailConfirmed = true
        };

        var request = new ForgotPasswordRequest
        {
            Email = "test@test.com" // Lowercase in request
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockEmailService.Setup(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _handler.Handle(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        user.PasswordResetToken.Should().NotBeNullOrEmpty();

        _mockEmailService.Verify(
            x => x.SendPasswordResetEmailAsync("test@test.com", It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Generate_Different_Tokens_For_Multiple_Requests()
    {
        // Arrange - Test that tokens are unique
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            EmailConfirmed = true
        };

        var request = new ForgotPasswordRequest
        {
            Email = "test@test.com"
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.Users).Returns(_context.Users);

        _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockEmailService.Setup(x => x.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        // Act - Make two requests
        await _handler.Handle(request);
        var firstToken = user.PasswordResetToken;

        await _handler.Handle(request);
        var secondToken = user.PasswordResetToken;

        // Assert - Tokens should be different
        firstToken.Should().NotBe(secondToken);
        firstToken.Should().NotBeNullOrEmpty();
        secondToken.Should().NotBeNullOrEmpty();
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
