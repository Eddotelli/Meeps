using API.Features.Auth.CompleteRegistration;
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
using Shared.Enums;
using Xunit;

namespace API.Tests.Features.Auth.CompleteRegistration;

/// <summary>
/// Unit tests for CompleteRegistrationHandler.
/// Tests profile completion including password setting, location detection, and token generation.
/// </summary>
public class CompleteRegistrationHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<User>> _mockUserManager;
    private readonly Mock<ITokenService> _mockTokenService;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<CompleteRegistrationHandler>> _mockLogger;
    private readonly Mock<ILocationDetectionService> _mockLocationService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly CompleteRegistrationHandler _handler;

    public CompleteRegistrationHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _mockUserManager = MockUserManager<User>();
        _mockTokenService = new Mock<ITokenService>();
        _mockEmailService = new Mock<IEmailService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<CompleteRegistrationHandler>>();
        _mockLocationService = new Mock<ILocationDetectionService>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        _mockConfiguration.Setup(x => x["Jwt:RefreshTokenExpirationDays"]).Returns("7");
        _mockConfiguration.Setup(x => x["Jwt:AccessTokenExpirationMinutes"]).Returns("15");

        var httpContext = new DefaultHttpContext();
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        _handler = new CompleteRegistrationHandler(
            _mockUserManager.Object,
            _context,
            _mockTokenService.Object,
            _mockEmailService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockLocationService.Object,
            _mockHttpContextAccessor.Object
        );
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Token_Invalid()
    {
        // Arrange
        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "invalid-token",
            DisplayName = "TestUser",
            Password = "ValidPass123!",
            AcceptTerms = true
        };

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EmailErrors.InvalidToken);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Token_Expired()
    {
        // Arrange
        var user = new User
        {
            Email = "test@test.com",
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(-1), // Expired
            EmailConfirmed = true
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            DisplayName = "TestUser",
            Password = "ValidPass123!",
            AcceptTerms = true
        };

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EmailErrors.InvalidToken);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Email_Not_Confirmed()
    {
        // Arrange
        var user = new User
        {
            Email = "test@test.com",
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(1),
            EmailConfirmed = false // Not confirmed
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            DisplayName = "TestUser",
            Password = "ValidPass123!",
            AcceptTerms = true
        };

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.EmailNotVerified);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_Profile_Already_Complete()
    {
        // Arrange
        var user = new User
        {
            Email = "test@test.com",
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(1),
            EmailConfirmed = true
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.HasPasswordAsync(user))
            .ReturnsAsync(true); // Already has password

        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            DisplayName = "TestUser",
            Password = "ValidPass123!",
            AcceptTerms = true
        };

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.ProfileAlreadyComplete);
    }

    [Fact]
    public async Task HandleAsync_Should_Return_Failure_When_DisplayName_Already_Exists()
    {
        // Arrange
        var existingUser = new User
        {
            DisplayName = "ExistingUser",
            Email = "existing@test.com"
        };
        _context.Users.Add(existingUser);

        var user = new User
        {
            Email = "test@test.com",
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(1),
            EmailConfirmed = true
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.HasPasswordAsync(user))
            .ReturnsAsync(false);

        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            DisplayName = "ExistingUser", // Already taken
            Password = "ValidPass123!",
            AcceptTerms = true
        };

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.DisplayNameAlreadyExists);
    }

    [Fact]
    public async Task HandleAsync_Should_Complete_Registration_Successfully()
    {
        // Arrange
        var user = new User
        {
            Email = "test@test.com",
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(1),
            EmailConfirmed = true
        };
        _context.Users.Add(user);

        var category = new Category { Id = 1, Type = Shared.Enums.CategoryType.Sports };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.HasPasswordAsync(user))
            .ReturnsAsync(false);
        _mockUserManager.Setup(x => x.AddPasswordAsync(user, It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.AddToRoleAsync(user, It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });
        _mockTokenService.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("access-token");
        _mockTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");
        _mockLocationService.Setup(x => x.DetectLocationFromIP(It.IsAny<string>()))
            .ReturnsAsync(new Shared.Contracts.Locations.LocationSearchResult
            {
                DisplayName = "Stockholm, Sweden",
                City = "Stockholm",
                Country = "Sweden",
                CountryCode = "SE",
                Latitude = 59.3293,
                Longitude = 18.0686
            });

        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            DisplayName = "NewUser",
            Password = "ValidPass123!",
            AcceptTerms = true,
            BirthDate = new DateTime(1990, 1, 1),
            Gender = Gender.Male,
            CategoryIds = new int[] { 1 }
        };

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Response.Should().NotBeNull();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("refresh-token");

        var updatedUser = await _context.Users.FirstAsync(u => u.Email == user.Email);
        updatedUser.DisplayName.Should().Be("NewUser");
        updatedUser.UserName.Should().Be("NewUser");
        updatedUser.EmailVerificationToken.Should().BeNull();
        updatedUser.DefaultCity.Should().Be("Stockholm");

        var userCategories = await _context.UserCategories.Where(uc => uc.UserId == updatedUser.Id).ToListAsync();
        userCategories.Should().HaveCount(1);
        userCategories.First().CategoryId.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_Should_Set_Default_Location_From_IP()
    {
        // Arrange
        var user = new User
        {
            Email = "test@test.com",
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(1),
            EmailConfirmed = true
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _mockUserManager.Setup(x => x.HasPasswordAsync(user)).ReturnsAsync(false);
        _mockUserManager.Setup(x => x.AddPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.AddToRoleAsync(user, It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "User" });
        _mockTokenService.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>())).Returns("token");
        _mockTokenService.Setup(x => x.GenerateRefreshToken()).Returns("refresh");

        _mockLocationService.Setup(x => x.DetectLocationFromIP(It.IsAny<string>()))
            .ReturnsAsync(new Shared.Contracts.Locations.LocationSearchResult
            {
                DisplayName = "Gothenburg, Sweden",
                City = "Gothenburg",
                Latitude = 57.7089,
                Longitude = 11.9746
            });

        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            DisplayName = "TestUser",
            Password = "ValidPass123!",
            AcceptTerms = true
        };

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updatedUser = await _context.Users.FirstAsync(u => u.Email == user.Email);
        updatedUser.DefaultCity.Should().Be("Gothenburg");
        updatedUser.DefaultCityLatitude.Should().Be(57.7089);
        updatedUser.DefaultCityLongitude.Should().Be(11.9746);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private static Mock<UserManager<TUser>> MockUserManager<TUser>() where TUser : class
    {
        var store = new Mock<IUserStore<TUser>>();
        var passwordHasher = new Mock<IPasswordHasher<TUser>>();
        var userValidators = new List<IUserValidator<TUser>>();
        var passwordValidators = new List<IPasswordValidator<TUser>>();
        var keyNormalizer = new Mock<ILookupNormalizer>();
        var errors = new Mock<IdentityErrorDescriber>();
        var services = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<UserManager<TUser>>>();

        return new Mock<UserManager<TUser>>(
            store.Object,
            null!,
            passwordHasher.Object,
            userValidators,
            passwordValidators,
            keyNormalizer.Object,
            errors.Object,
            services.Object,
            logger.Object);
    }
}
