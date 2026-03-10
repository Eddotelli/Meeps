using API.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace API.Tests.Infrastructure.Services;

/// <summary>
/// Unit tests for FakeEmailService.
/// Tests email mock functionality used in testing environment.
/// </summary>
public class FakeEmailServiceTests
{
    private readonly Mock<ILogger<FakeEmailService>> _mockLogger;
    private readonly FakeEmailService _service;

    public FakeEmailServiceTests()
    {
        _mockLogger = new Mock<ILogger<FakeEmailService>>();
        _service = new FakeEmailService(_mockLogger.Object);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_Should_Return_Success()
    {
        // Arrange
        var email = "test@test.com";
        var token = "test-token-123";
        var userId = "1";

        // Act
        var result = await _service.SendVerificationEmailAsync(email, token, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SendVerificationEmailAsync_Should_Log_Email_Details()
    {
        // Arrange
        var email = "test@test.com";
        var token = "test-token-123";
        var userId = "1";

        // Act
        await _service.SendVerificationEmailAsync(email, token, userId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[FAKE EMAIL]") && v.ToString()!.Contains(email)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_Should_Return_Success()
    {
        // Arrange
        var email = "test@test.com";
        var token = "reset-token-123";

        // Act
        var result = await _service.SendPasswordResetEmailAsync(email, token);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_Should_Log_Email_Details()
    {
        // Arrange
        var email = "test@test.com";
        var token = "reset-token-123";

        // Act
        await _service.SendPasswordResetEmailAsync(email, token);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[FAKE EMAIL]") && v.ToString()!.Contains(email)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_Should_Return_Success()
    {
        // Arrange
        var email = "test@test.com";
        var displayName = "Test User";

        // Act
        var result = await _service.SendWelcomeEmailAsync(email, displayName);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_Should_Log_Email_Details()
    {
        // Arrange
        var email = "test@test.com";
        var displayName = "Test User";

        // Act
        await _service.SendWelcomeEmailAsync(email, displayName);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[FAKE EMAIL]") && v.ToString()!.Contains(email) && v.ToString()!.Contains(displayName)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task All_Email_Methods_Should_Complete_Quickly()
    {
        // Arrange
        var email = "test@test.com";

        // Act & Assert - all should complete immediately
        var verificationTask = _service.SendVerificationEmailAsync(email, "token", "1");
        var resetTask = _service.SendPasswordResetEmailAsync(email, "token");
        var welcomeTask = _service.SendWelcomeEmailAsync(email, "User");

        await Task.WhenAll(verificationTask, resetTask, welcomeTask);

        verificationTask.IsCompleted.Should().BeTrue();
        resetTask.IsCompleted.Should().BeTrue();
        welcomeTask.IsCompleted.Should().BeTrue();
    }
}
