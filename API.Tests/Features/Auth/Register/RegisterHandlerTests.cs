using API.Features.Auth.Register;
using API.Infrastructure.Services;
using API.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Auth;
using Xunit;

namespace API.Tests.Features.Auth.Register;

/// <summary>
/// Unit tests for RegisterHandler.
/// Tests user registration business logic including database operations and email sending.
/// </summary>
public class RegisterHandlerTests
{
    private readonly Mock<UserManager<User>> _mockUserManager;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ITokenService> _mockTokenService;
    private readonly Mock<ILogger<RegisterHandler>> _mockLogger;
    private readonly RegisterHandler _handler;

    public RegisterHandlerTests()
    {
        _mockUserManager = MockUserManager<User>();
        _mockEmailService = new Mock<IEmailService>();
        _mockTokenService = new Mock<ITokenService>();
        _mockLogger = new Mock<ILogger<RegisterHandler>>();

        _handler = new RegisterHandler(
            _mockUserManager.Object,
            _mockEmailService.Object,
            _mockTokenService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Success_With_Valid_Email()
    {
        // Arrange
        var request = new RegisterRequest { Email = "test@test.com" };
        var token = "test-token-123";

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);

        _mockTokenService.Setup(x => x.GenerateEmailVerificationToken())
            .Returns(token);

        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockEmailService.Setup(x => x.SendVerificationEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Message.Should().Contain("verification email");

        _mockUserManager.Verify(x => x.CreateAsync(It.Is<User>(u =>
            u.Email == request.Email &&
            u.EmailVerificationToken == token &&
            !u.EmailConfirmed)), Times.Once);

        _mockEmailService.Verify(x => x.SendVerificationEmailAsync(
            request.Email,
            token,
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Email_Already_Exists()
    {
        // Arrange
        var request = new RegisterRequest { Email = "existing@test.com" };
        var existingUser = new User { Email = request.Email };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.EmailAlreadyExists);

        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<User>()), Times.Never);
        _mockEmailService.Verify(x => x.SendVerificationEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_User_Creation_Fails()
    {
        // Arrange
        var request = new RegisterRequest { Email = "test@test.com" };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);

        _mockTokenService.Setup(x => x.GenerateEmailVerificationToken())
            .Returns("test-token");

        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Database error" }));

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.RegistrationFailed);

        _mockEmailService.Verify(x => x.SendVerificationEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_Rollback_User_When_Email_Sending_Fails()
    {
        // Arrange
        var request = new RegisterRequest { Email = "test@test.com" };
        var createdUser = new User { Email = request.Email };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);

        _mockTokenService.Setup(x => x.GenerateEmailVerificationToken())
            .Returns("test-token");

        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<User>(user => createdUser = user);

        _mockEmailService.Setup(x => x.SendVerificationEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .ReturnsAsync(Result.Failure(EmailErrors.SendFailed));

        _mockUserManager.Setup(x => x.DeleteAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EmailErrors.SendFailed);

        _mockUserManager.Verify(x => x.DeleteAsync(It.IsAny<User>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_Set_Token_Expiry_24_Hours_From_Now()
    {
        // Arrange
        var request = new RegisterRequest { Email = "test@test.com" };
        User? capturedUser = null;

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);

        _mockTokenService.Setup(x => x.GenerateEmailVerificationToken())
            .Returns("test-token");

        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<User>(user => capturedUser = user);

        _mockEmailService.Setup(x => x.SendVerificationEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        // Act
        await _handler.HandleAsync(request);

        // Assert
        capturedUser.Should().NotBeNull();
        capturedUser!.EmailVerificationTokenExpiry.Should().NotBeNull();
        capturedUser.EmailVerificationTokenExpiry.Should().BeCloseTo(
            DateTime.UtcNow.AddHours(24),
            TimeSpan.FromSeconds(5));
    }

    // Helper method to mock UserManager
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
