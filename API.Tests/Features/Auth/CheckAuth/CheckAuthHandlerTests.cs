using API.Features.Auth.CheckAuth;
using API.Infrastructure.Data;
using API.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shared.Contracts.Auth;
using System.Security.Claims;
using Xunit;

namespace API.Tests.Features.Auth.CheckAuth;

/// <summary>
/// Unit tests for CheckAuthHandler.
/// Tests authentication status checking and user info retrieval.
/// </summary>
public class CheckAuthHandlerTests
{
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly ApplicationDbContext _context;
    private readonly CheckAuthHandler _handler;

    public CheckAuthHandlerTests()
    {
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _handler = new CheckAuthHandler(_mockHttpContextAccessor.Object, _context);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_User_Not_Authenticated()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AUTH.NOT_AUTHENTICATED");
        result.Error.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_HttpContext_Is_Null()
    {
        // Arrange
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AUTH.NOT_AUTHENTICATED");
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_UserId_Claim_Missing()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, "test@test.com"),
            new Claim(ClaimTypes.Name, "Test User")
            // Missing NameIdentifier (UserId)
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AUTH.INVALID_CLAIMS");
        result.Error.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Email_Claim_Missing()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "Test User")
            // Missing Email
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AUTH.INVALID_CLAIMS");
    }

    [Fact]
    public async Task Handle_Should_Return_Success_With_Valid_Claims()
    {
        // Arrange
        var user = new User
        {
            Id = 123,
            Email = "test@test.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "123"),
            new Claim(ClaimTypes.Email, "test@test.com"),
            new Claim(ClaimTypes.Name, "Test User")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.UserId.Should().Be(123);
        result.Value.Email.Should().Be("test@test.com");
        result.Value.DisplayName.Should().Be("Test User");
    }

    [Fact]
    public async Task Handle_Should_Return_Success_Without_DisplayName()
    {
        // Arrange - DisplayName is optional
        var user = new User
        {
            Id = 456,
            Email = "user@test.com",
            DisplayName = "",
            PasswordHash = "hash",
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "456"),
            new Claim(ClaimTypes.Email, "user@test.com")
            // No DisplayName
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(456);
        result.Value.Email.Should().Be("user@test.com");
        result.Value.DisplayName.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Parse_UserId_Correctly()
    {
        // Arrange
        var user = new User
        {
            Id = 999,
            Email = "test@test.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "999"),
            new Claim(ClaimTypes.Email, "test@test.com"),
            new Claim(ClaimTypes.Name, "Test User")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(999);
        result.Value.UserId.GetType().Should().Be(typeof(int));
    }

    [Fact]
    public async Task Handle_Should_Handle_Empty_String_Claims()
    {
        // Arrange - Empty strings should be treated as missing
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, ""),
            new Claim(ClaimTypes.Email, ""),
            new Claim(ClaimTypes.Name, "")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("AUTH.INVALID_CLAIMS");
    }

    [Fact]
    public async Task Handle_Should_Work_With_Different_Auth_Schemes()
    {
        // Arrange - Test with Bearer authentication scheme
        var user = new User
        {
            Id = 1,
            Email = "bearer@test.com",
            DisplayName = "Bearer User",
            PasswordHash = "hash",
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Email, "bearer@test.com"),
            new Claim(ClaimTypes.Name, "Bearer User")
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("bearer@test.com");
    }

    [Fact]
    public async Task Handle_Should_Return_First_Claim_Value_When_Multiple_Exist()
    {
        // Arrange - Multiple claims of same type (edge case)
        var user = new User
        {
            Id = 1,
            Email = "first@test.com",
            DisplayName = "First Name",
            PasswordHash = "hash",
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.NameIdentifier, "2"), // Duplicate
            new Claim(ClaimTypes.Email, "first@test.com"),
            new Claim(ClaimTypes.Email, "second@test.com"), // Duplicate
            new Claim(ClaimTypes.Name, "First Name")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(1); // First value
        result.Value.Email.Should().Be("first@test.com"); // First value
    }
}
