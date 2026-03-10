using API.Features.Auth.RefreshToken;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using API.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Common.Errors;
using Xunit;

namespace API.Tests.Features.Auth.RefreshToken;

/// <summary>
/// Unit tests for RefreshTokenHandler.
/// Tests token refresh logic including validation, expiry, revocation, and security measures.
/// </summary>
public class RefreshTokenHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<User>> _mockUserManager;
    private readonly Mock<ITokenService> _mockTokenService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<RefreshTokenHandler>> _mockLogger;
    private readonly RefreshTokenHandler _handler;

    public RefreshTokenHandlerTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _mockUserManager = MockUserManager<User>();
        _mockTokenService = new Mock<ITokenService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<RefreshTokenHandler>>();

        // Setup configuration defaults
        _mockConfiguration.Setup(x => x["Jwt:RefreshTokenExpirationDays"]).Returns("7");
        _mockConfiguration.Setup(x => x["Jwt:RefreshTokenExpirationDaysRememberMe"]).Returns("30");
        _mockConfiguration.Setup(x => x["Jwt:SlidingExpirationThresholdDays"]).Returns("3");
        _mockConfiguration.Setup(x => x["Jwt:RefreshTokenAbsoluteMaxDays"]).Returns("90");

        _handler = new RefreshTokenHandler(
            _mockUserManager.Object,
            _context,
            _mockTokenService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Token_Not_Found()
    {
        // Arrange
        var nonExistentToken = "non-existent-token";

        // Act
        var result = await _handler.HandleAsync(nonExistentToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TokenErrors.NotFound);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Token_Is_Expired()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(user);

        var expiredToken = new Models.RefreshToken
        {
            Token = "expired-token",
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired yesterday
            IsRevoked = false,
            RememberMe = false
        };

        await _context.RefreshTokens.AddAsync(expiredToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _handler.HandleAsync("expired-token");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TokenErrors.Expired);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Token_Is_Revoked()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(user);

        var revokedToken = new Models.RefreshToken
        {
            Token = "revoked-token",
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(6),
            IsRevoked = true,
            RevokedAt = DateTime.UtcNow.AddHours(-1),
            RememberMe = false
        };

        await _context.RefreshTokens.AddAsync(revokedToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _handler.HandleAsync("revoked-token");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TokenErrors.Revoked);
    }

    [Fact]
    public async Task HandleAsync_Should_Revoke_All_User_Tokens_When_Revoked_Token_Is_Reused()
    {
        // Arrange - This tests the security feature that detects token theft
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(user);

        // User has 3 active tokens
        var activeToken1 = new Models.RefreshToken
        {
            Token = "active-token-1",
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            RememberMe = false
        };

        var activeToken2 = new Models.RefreshToken
        {
            Token = "active-token-2",
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            RememberMe = false
        };

        var revokedToken = new Models.RefreshToken
        {
            Token = "revoked-token",
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(6),
            IsRevoked = true,
            RevokedAt = DateTime.UtcNow.AddHours(-1),
            RememberMe = false
        };

        await _context.RefreshTokens.AddRangeAsync(activeToken1, activeToken2, revokedToken);
        await _context.SaveChangesAsync();

        // Act - Attempt to reuse revoked token (indicates potential theft)
        var result = await _handler.HandleAsync("revoked-token");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TokenErrors.Revoked);

        // All active tokens should now be revoked
        var allTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ToListAsync();

        allTokens.Should().AllSatisfy(token => token.IsRevoked.Should().BeTrue());
        allTokens.Should().AllSatisfy(token => token.RevokedAt.Should().NotBeNull());
    }

    [Fact]
    public async Task HandleAsync_Should_Generate_New_Tokens_For_Valid_Refresh_Token()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(user);

        var validToken = new Models.RefreshToken
        {
            Token = "valid-token",
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            RememberMe = false
        };

        await _context.RefreshTokens.AddAsync(validToken);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });

        _mockTokenService.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("new-access-token");

        _mockTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("new-refresh-token");

        // Act
        var result = await _handler.HandleAsync("valid-token");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access-token");
        result.Value.NewRefreshToken.Should().Be("new-refresh-token");
        result.Value.Response.Should().NotBeNull();

        _mockTokenService.Verify(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()), Times.Once);
        _mockTokenService.Verify(x => x.GenerateRefreshToken(), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_Revoke_Old_Token_And_Store_New_Token()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(user);

        var oldToken = new Models.RefreshToken
        {
            Token = "old-token",
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            RememberMe = false
        };

        await _context.RefreshTokens.AddAsync(oldToken);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });

        _mockTokenService.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("new-access-token");

        _mockTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("new-refresh-token");

        // Act
        var result = await _handler.HandleAsync("old-token");

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Old token should be revoked
        var revokedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == "old-token");

        revokedToken.Should().NotBeNull();
        revokedToken!.IsRevoked.Should().BeTrue();
        revokedToken.RevokedAt.Should().NotBeNull();
        revokedToken.ReplacedByToken.Should().Be("new-refresh-token");

        // New token should be stored
        var newToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == "new-refresh-token");

        newToken.Should().NotBeNull();
        newToken!.UserId.Should().Be(user.Id);
        newToken.IsRevoked.Should().BeFalse();
        newToken.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task HandleAsync_Should_Extend_Expiry_When_Token_Within_Sliding_Window()
    {
        // Arrange - Token expires in 2 days (within 3-day threshold)
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(user);

        var tokenExpiringWithinThreshold = new Models.RefreshToken
        {
            Token = "expiring-soon-token",
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            ExpiresAt = DateTime.UtcNow.AddDays(2), // Within 3-day threshold
            IsRevoked = false,
            RememberMe = false
        };

        await _context.RefreshTokens.AddAsync(tokenExpiringWithinThreshold);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });

        _mockTokenService.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("new-access-token");

        _mockTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("new-refresh-token");

        // Act
        var result = await _handler.HandleAsync("expiring-soon-token");

        // Assert
        result.IsSuccess.Should().BeTrue();

        var newToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == "new-refresh-token");

        newToken.Should().NotBeNull();
        // New token should have full 7 days expiry (extended)
        newToken!.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task HandleAsync_Should_Not_Extend_Expiry_When_Token_Outside_Sliding_Window()
    {
        // Arrange - Token expires in 5 days (outside 3-day threshold)
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(user);

        var originalExpiry = DateTime.UtcNow.AddDays(5);
        var tokenNotExpiringSoon = new Models.RefreshToken
        {
            Token = "not-expiring-soon-token",
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = originalExpiry, // Outside 3-day threshold
            IsRevoked = false,
            RememberMe = false
        };

        await _context.RefreshTokens.AddAsync(tokenNotExpiringSoon);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });

        _mockTokenService.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("new-access-token");

        _mockTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("new-refresh-token");

        // Act
        var result = await _handler.HandleAsync("not-expiring-soon-token");

        // Assert
        result.IsSuccess.Should().BeTrue();

        var newToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == "new-refresh-token");

        newToken.Should().NotBeNull();
        // New token should keep original expiry (not extended)
        newToken!.ExpiresAt.Should().BeCloseTo(originalExpiry, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task HandleAsync_Should_Preserve_RememberMe_Flag()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(user);

        var tokenWithRememberMe = new Models.RefreshToken
        {
            Token = "remember-me-token",
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = false,
            RememberMe = true // RememberMe is enabled
        };

        await _context.RefreshTokens.AddAsync(tokenWithRememberMe);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });

        _mockTokenService.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("new-access-token");

        _mockTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("new-refresh-token");

        // Act
        var result = await _handler.HandleAsync("remember-me-token");

        // Assert
        result.IsSuccess.Should().BeTrue();

        var newToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == "new-refresh-token");

        newToken.Should().NotBeNull();
        newToken!.RememberMe.Should().BeTrue(); // Flag should be preserved
    }

    [Fact]
    public async Task HandleAsync_Should_Use_Extended_Expiry_For_RememberMe_Tokens()
    {
        // Arrange - RememberMe token within sliding window
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(user);

        var rememberMeToken = new Models.RefreshToken
        {
            Token = "remember-me-token",
            UserId = user.Id,
            User = user,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = DateTime.UtcNow.AddDays(2), // Within sliding window
            IsRevoked = false,
            RememberMe = true
        };

        await _context.RefreshTokens.AddAsync(rememberMeToken);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });

        _mockTokenService.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("new-access-token");

        _mockTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("new-refresh-token");

        // Act
        var result = await _handler.HandleAsync("remember-me-token");

        // Assert
        result.IsSuccess.Should().BeTrue();

        var newToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == "new-refresh-token");

        newToken.Should().NotBeNull();
        // Should use 30-day expiry for RememberMe tokens (not 7 days)
        newToken!.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
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
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
