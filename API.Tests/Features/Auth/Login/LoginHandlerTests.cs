using API.Features.Auth.Login;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using API.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Common.Errors;
using Shared.Contracts.Auth;
using Xunit;

namespace API.Tests.Features.Auth.Login;

/// <summary>
/// Unit tests for LoginHandler.
/// Tests login business logic including password verification and token generation.
/// </summary>
public class LoginHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<User>> _mockUserManager;
    private readonly Mock<SignInManager<User>> _mockSignInManager;
    private readonly Mock<ITokenService> _mockTokenService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<LoginHandler>> _mockLogger;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _mockUserManager = MockUserManager<User>();
        _mockSignInManager = MockSignInManager(_mockUserManager);
        _mockTokenService = new Mock<ITokenService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<LoginHandler>>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        // Setup HttpContext
        var httpContext = new DefaultHttpContext();
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Setup configuration
        _mockConfiguration.Setup(x => x["Jwt:RefreshTokenExpirationDays"]).Returns("7");

        _handler = new LoginHandler(
            _mockUserManager.Object,
            _mockSignInManager.Object,
            _context,
            _mockTokenService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockHttpContextAccessor.Object
        );
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_User_Not_Found()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "notfound@test.com",
            Password = "Password123!"
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Email_Not_Verified()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "unverified@test.com",
            Password = "Password123!"
        };

        var user = new User
        {
            Email = request.Email,
            EmailConfirmed = false
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.EmailNotVerified);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Profile_Not_Complete()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "incomplete@test.com",
            Password = "Password123!"
        };

        var user = new User
        {
            Email = request.Email,
            EmailConfirmed = true
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.HasPasswordAsync(user))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.ProfileNotComplete);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Account_Locked_Out()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "locked@test.com",
            Password = "Password123!"
        };

        var user = new User
        {
            Email = request.Email,
            EmailConfirmed = true
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.HasPasswordAsync(user))
            .ReturnsAsync(true);

        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, request.Password, true))
            .ReturnsAsync(SignInResult.LockedOut);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.LockedOut);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Password_Invalid()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@test.com",
            Password = "WrongPassword!"
        };

        var user = new User
        {
            Email = request.Email,
            EmailConfirmed = true
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.HasPasswordAsync(user))
            .ReturnsAsync(true);

        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, request.Password, true))
            .ReturnsAsync(SignInResult.Failed);

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidCredentials);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Success_With_Valid_Credentials()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "valid@test.com",
            Password = "ValidPassword123!"
        };

        var user = new User
        {
            Id = 1,
            Email = request.Email,
            UserName = "validuser",
            DisplayName = "Valid User",
            EmailConfirmed = true
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.HasPasswordAsync(user))
            .ReturnsAsync(true);

        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, request.Password, true))
            .ReturnsAsync(SignInResult.Success);

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });

        _mockTokenService.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("access-token-123");

        _mockTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token-123");

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Response.Should().NotBeNull();
        result.Value.Response.Email.Should().Be(request.Email);
        result.Value.AccessToken.Should().Be("access-token-123");
        result.Value.RefreshToken.Should().Be("refresh-token-123");

        _mockTokenService.Verify(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()), Times.Once);
        _mockTokenService.Verify(x => x.GenerateRefreshToken(), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_Store_Refresh_Token_In_Database()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@test.com",
            Password = "Password123!",
            RememberMe = false
        };

        var user = new User
        {
            Id = 1,
            Email = request.Email,
            UserName = "testuser",
            DisplayName = "Test User",
            EmailConfirmed = true
        };

        _mockUserManager.Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockUserManager.Setup(x => x.HasPasswordAsync(user))
            .ReturnsAsync(true);

        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, request.Password, true))
            .ReturnsAsync(SignInResult.Success);

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });

        _mockTokenService.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("access-token");

        _mockTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.UserId == user.Id && t.Token == "refresh-token");

        storedToken.Should().NotBeNull();
        storedToken!.IsRevoked.Should().BeFalse();
        storedToken.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
    }

    // Helper methods
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

    private static Mock<SignInManager<User>> MockSignInManager(Mock<UserManager<User>> userManager)
    {
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<User>>();

        return new Mock<SignInManager<User>>(
            userManager.Object,
            contextAccessor.Object,
            claimsFactory.Object,
            null!, null!, null!, null!);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
