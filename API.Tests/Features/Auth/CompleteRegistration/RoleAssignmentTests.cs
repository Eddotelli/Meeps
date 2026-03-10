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
using Shared.Contracts.Auth;
using Shared.Enums;
using Xunit;

namespace API.Tests.Features.Auth.CompleteRegistration;

/// <summary>
/// Tests for automatic role assignment during user registration.
/// Verifies that new users receive the User role upon completing registration.
/// </summary>
public class RoleAssignmentTests : IDisposable
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

    public RoleAssignmentTests()
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

        _mockConfiguration.Setup(x => x["Jwt:RefreshTokenExpirationDays"]).Returns("30");
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
    public async Task CompleteRegistration_Should_Assign_User_Role_To_New_User()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "test@test.com",
            EmailConfirmed = true,
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(1)
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            DisplayName = "TestUser",
            Password = "ValidPass123!",
            BirthDate = new DateTime(1990, 1, 1),
            Gender = Gender.Male,
            AcceptTerms = true,
            CategoryIds = Array.Empty<int>()
        };

        // Mock UserManager methods
        _mockUserManager.Setup(x => x.HasPasswordAsync(user))
            .ReturnsAsync(false);

        _mockUserManager.Setup(x => x.AddPasswordAsync(user, request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.AddToRoleAsync(user, UserRole.User.ToString()))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { UserRole.User.ToString() });

        _mockTokenService.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("mock-access-token");

        _mockTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("mock-refresh-token");

        _mockEmailService.Setup(x => x.SendWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Shared.Common.Results.Result.Success());

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Verify that AddToRoleAsync was called with the User role
        _mockUserManager.Verify(
            x => x.AddToRoleAsync(user, UserRole.User.ToString()),
            Times.Once,
            "User role should be assigned to new user"
        );
    }

    [Fact]
    public async Task CompleteRegistration_Should_Fail_When_Role_Assignment_Fails()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "test@test.com",
            EmailConfirmed = true,
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(1)
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            DisplayName = "TestUser",
            Password = "ValidPass123!",
            BirthDate = new DateTime(1990, 1, 1),
            Gender = Gender.Male,
            AcceptTerms = true,
            CategoryIds = Array.Empty<int>()
        };

        // Mock UserManager methods
        _mockUserManager.Setup(x => x.HasPasswordAsync(user))
            .ReturnsAsync(false);

        _mockUserManager.Setup(x => x.AddPasswordAsync(user, request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Simulate role assignment failure
        _mockUserManager.Setup(x => x.AddToRoleAsync(user, UserRole.User.ToString()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError 
            { 
                Code = "RoleAssignmentFailed", 
                Description = "Failed to assign role" 
            }));

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteRegistration_Should_Include_Role_In_JWT_Token()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "test@test.com",
            EmailConfirmed = true,
            EmailVerificationToken = "valid-token",
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(1)
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var request = new CompleteRegistrationRequest
        {
            VerificationToken = "valid-token",
            DisplayName = "TestUser",
            Password = "ValidPass123!",
            BirthDate = new DateTime(1990, 1, 1),
            Gender = Gender.Male,
            AcceptTerms = true,
            CategoryIds = Array.Empty<int>()
        };

        var assignedRoles = new List<string> { UserRole.User.ToString() };

        // Mock UserManager methods
        _mockUserManager.Setup(x => x.HasPasswordAsync(user))
            .ReturnsAsync(false);

        _mockUserManager.Setup(x => x.AddPasswordAsync(user, request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.AddToRoleAsync(user, UserRole.User.ToString()))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(assignedRoles);

        _mockTokenService.Setup(x => x.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("mock-access-token");

        _mockTokenService.Setup(x => x.GenerateRefreshToken())
            .Returns("mock-refresh-token");

        _mockEmailService.Setup(x => x.SendWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Shared.Common.Results.Result.Success());

        // Act
        var result = await _handler.HandleAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        // Verify that GetRolesAsync was called to include roles in token
        _mockUserManager.Verify(
            x => x.GetRolesAsync(user),
            Times.Once,
            "Roles should be retrieved for JWT token generation"
        );

        // Verify that GenerateAccessToken was called with the roles
        _mockTokenService.Verify(
            x => x.GenerateAccessToken(user, It.Is<IList<string>>(roles => 
                roles.Contains(UserRole.User.ToString()))),
            Times.Once,
            "Access token should include the User role"
        );
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

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
