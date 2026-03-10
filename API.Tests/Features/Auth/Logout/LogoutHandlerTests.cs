using API.Features.Auth.Logout;
using API.Infrastructure.Data;
using API.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Common.Errors;
using System.Security.Claims;
using Xunit;
using RefreshTokenModel = API.Models.RefreshToken;

namespace API.Tests.Features.Auth.Logout;

/// <summary>
/// Unit tests for LogoutHandler.
/// Tests logout logic including token revocation.
/// </summary>
public class LogoutHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<LogoutHandler>> _mockLogger;
    private readonly LogoutHandler _handler;

    public LogoutHandlerTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _mockLogger = new Mock<ILogger<LogoutHandler>>();

        _handler = new LogoutHandler(
            _context,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_UserId_Claim_Missing()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, "test@test.com")
            // Missing NameIdentifier (UserId)
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = await _handler.HandleAsync(user);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CommonErrors.Unauthorized);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_UserId_Invalid()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "not-a-number") // Invalid userId
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = await _handler.HandleAsync(user);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CommonErrors.Unauthorized);
    }

    [Fact]
    public async Task HandleAsync_Should_Revoke_All_Active_Tokens()
    {
        // Arrange
        var userId = 1;
        var dbUser = new User
        {
            Id = userId,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(dbUser);

        // User has 3 active tokens
        var activeToken1 = new RefreshTokenModel
        {
            Token = "active-token-1",
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        var activeToken2 = new RefreshTokenModel
        {
            Token = "active-token-2",
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        var activeToken3 = new RefreshTokenModel
        {
            Token = "active-token-3",
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        await _context.RefreshTokens.AddRangeAsync(activeToken1, activeToken2, activeToken3);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = await _handler.HandleAsync(user);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // All tokens should be revoked
        var allTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync();

        allTokens.Should().HaveCount(3);
        allTokens.Should().AllSatisfy(token =>
        {
            token.IsRevoked.Should().BeTrue();
            token.RevokedAt.Should().NotBeNull();
            token.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        });
    }

    [Fact]
    public async Task HandleAsync_Should_Not_Revoke_Already_Revoked_Tokens()
    {
        // Arrange
        var userId = 1;
        var dbUser = new User
        {
            Id = userId,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(dbUser);

        // User has 1 active and 1 already revoked token
        var activeToken = new RefreshTokenModel
        {
            Token = "active-token",
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        var revokedToken = new RefreshTokenModel
        {
            Token = "revoked-token",
            UserId = userId,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(6),
            IsRevoked = true,
            RevokedAt = DateTime.UtcNow.AddHours(-1)
        };

        await _context.RefreshTokens.AddRangeAsync(activeToken, revokedToken);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = await _handler.HandleAsync(user);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Only active token should be revoked
        var allTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync();

        allTokens.Should().HaveCount(2);
        allTokens.Should().AllSatisfy(token => token.IsRevoked.Should().BeTrue());

        // Revoked token should keep original RevokedAt timestamp
        var alreadyRevoked = allTokens.First(t => t.Token == "revoked-token");
        alreadyRevoked.RevokedAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(-1), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task HandleAsync_Should_Not_Revoke_Expired_Tokens()
    {
        // Arrange
        var userId = 1;
        var dbUser = new User
        {
            Id = userId,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(dbUser);

        // User has 1 active and 1 expired token
        var activeToken = new RefreshTokenModel
        {
            Token = "active-token",
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        var expiredToken = new RefreshTokenModel
        {
            Token = "expired-token",
            UserId = userId,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = DateTime.UtcNow.AddDays(-3), // Expired 3 days ago
            IsRevoked = false
        };

        await _context.RefreshTokens.AddRangeAsync(activeToken, expiredToken);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = await _handler.HandleAsync(user);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Only active token should be revoked
        var allTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync();

        var active = allTokens.First(t => t.Token == "active-token");
        active.IsRevoked.Should().BeTrue();

        var expired = allTokens.First(t => t.Token == "expired-token");
        expired.IsRevoked.Should().BeFalse(); // Expired tokens are not touched
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Success_When_No_Active_Tokens()
    {
        // Arrange - User has no tokens
        var userId = 1;
        var dbUser = new User
        {
            Id = userId,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(dbUser);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = await _handler.HandleAsync(user);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync();

        tokens.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Should_Not_Affect_Other_Users_Tokens()
    {
        // Arrange - Two users with tokens
        var user1 = new User
        {
            Id = 1,
            Email = "user1@test.com",
            UserName = "user1",
            DisplayName = "User 1"
        };

        var user2 = new User
        {
            Id = 2,
            Email = "user2@test.com",
            UserName = "user2",
            DisplayName = "User 2"
        };

        await _context.Users.AddRangeAsync(user1, user2);

        var token1 = new RefreshTokenModel
        {
            Token = "user1-token",
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        var token2 = new RefreshTokenModel
        {
            Token = "user2-token",
            UserId = 2,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };

        await _context.RefreshTokens.AddRangeAsync(token1, token2);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1") // User 1 logs out
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        // Act - User 1 logs out
        var result = await _handler.HandleAsync(claimsPrincipal);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // User 1's token should be revoked
        var user1Token = await _context.RefreshTokens.FirstAsync(rt => rt.Token == "user1-token");
        user1Token.IsRevoked.Should().BeTrue();

        // User 2's token should remain active
        var user2Token = await _context.RefreshTokens.FirstAsync(rt => rt.Token == "user2-token");
        user2Token.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_Should_Handle_Large_Number_Of_Tokens()
    {
        // Arrange - User has many tokens (e.g., multiple devices)
        var userId = 1;
        var dbUser = new User
        {
            Id = userId,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };

        await _context.Users.AddAsync(dbUser);

        var tokens = Enumerable.Range(1, 10).Select(i => new RefreshTokenModel
        {
            Token = $"token-{i}",
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        }).ToList();

        await _context.RefreshTokens.AddRangeAsync(tokens);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = await _handler.HandleAsync(user);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var allTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync();

        allTokens.Should().HaveCount(10);
        allTokens.Should().AllSatisfy(token => token.IsRevoked.Should().BeTrue());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
