using API.Infrastructure.Services;
using API.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace API.Tests.Infrastructure.Services;

/// <summary>
/// Unit tests for TokenService.
/// Tests JWT token generation and refresh token creation.
/// </summary>
public class TokenServiceTests
{
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;

    public TokenServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string>
        {
            {"Jwt:Key", "ThisIsAVeryLongSecretKeyForTesting12345678901234567890"},
            {"Jwt:Issuer", "MeepsTestIssuer"},
            {"Jwt:Audience", "MeepsTestAudience"},
            {"Jwt:AccessTokenExpirationMinutes", "15"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _tokenService = new TokenService(_configuration);
    }

    [Fact]
    public void GenerateAccessToken_Should_Create_Valid_JWT()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User"
        };
        var roles = new List<string> { "User" };

        // Act
        var token = _tokenService.GenerateAccessToken(user, roles);

        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "1");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "test@test.com");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "testuser");
        jwtToken.Claims.Should().Contain(c => c.Type == "DisplayName" && c.Value == "Test User");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "User");
    }

    [Fact]
    public void GenerateAccessToken_Should_Set_Correct_Expiration()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser"
        };
        var roles = new List<string>();

        // Act
        var token = _tokenService.GenerateAccessToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var expectedExpiry = DateTime.UtcNow.AddMinutes(15);
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GenerateAccessToken_Should_Include_Multiple_Roles()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "admin@test.com",
            UserName = "adminuser"
        };
        var roles = new List<string> { "User", "Admin", "Moderator" };

        // Act
        var token = _tokenService.GenerateAccessToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        var roleClaims = jwtToken.Claims.Where(c => c.Type == ClaimTypes.Role).ToList();
        roleClaims.Should().HaveCount(3);
        roleClaims.Should().Contain(c => c.Value == "User");
        roleClaims.Should().Contain(c => c.Value == "Admin");
        roleClaims.Should().Contain(c => c.Value == "Moderator");
    }

    [Fact]
    public void GenerateAccessToken_Should_Include_JTI_Claim()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser"
        };
        var roles = new List<string>();

        // Act
        var token = _tokenService.GenerateAccessToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
    }

    [Fact]
    public void GenerateRefreshToken_Should_Return_Non_Empty_String()
    {
        // Act
        var token = _tokenService.GenerateRefreshToken();

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateRefreshToken_Should_Generate_Unique_Tokens()
    {
        // Act
        var token1 = _tokenService.GenerateRefreshToken();
        var token2 = _tokenService.GenerateRefreshToken();
        var token3 = _tokenService.GenerateRefreshToken();

        // Assert
        token1.Should().NotBe(token2);
        token2.Should().NotBe(token3);
        token1.Should().NotBe(token3);
    }

    [Fact]
    public void GenerateEmailVerificationToken_Should_Return_Non_Empty_String()
    {
        // Act
        var token = _tokenService.GenerateEmailVerificationToken();

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateEmailVerificationToken_Should_Be_URL_Safe()
    {
        // Act
        var token = _tokenService.GenerateEmailVerificationToken();

        // Assert - Should not contain +, /, or = characters (URL-safe base64)
        token.Should().NotContain("+");
        token.Should().NotContain("/");
        token.Should().NotContain("=");
    }

    [Fact]
    public void GenerateEmailVerificationToken_Should_Generate_Unique_Tokens()
    {
        // Act
        var token1 = _tokenService.GenerateEmailVerificationToken();
        var token2 = _tokenService.GenerateEmailVerificationToken();
        var token3 = _tokenService.GenerateEmailVerificationToken();

        // Assert
        token1.Should().NotBe(token2);
        token2.Should().NotBe(token3);
        token1.Should().NotBe(token3);
    }
}
