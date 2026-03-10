using API.Features.Users.GetUserProfile;
using API.Infrastructure.Data;
using API.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shared.Common.Errors;
using Shared.Enums;
using System.Security.Claims;
using Xunit;

namespace API.Tests.Features.Users.GetUserProfile;

/// <summary>
/// Unit tests for GetUserProfileHandler.
/// Tests profile retrieval logic and statistics calculation.
/// </summary>
public class GetUserProfileHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly GetUserProfileHandler _handler;

    public GetUserProfileHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        _handler = new GetUserProfileHandler(
            _context,
            _mockHttpContextAccessor.Object
        );
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_User_Not_Authenticated()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.Unauthorized);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_User_Not_Found()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "999")
        }));
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound);
    }

    [Fact]
    public async Task Handle_Should_Return_User_Profile_When_User_Exists()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User",
            Bio = "Test bio",
            Gender = Gender.Male,
            BirthDate = new DateTime(1990, 1, 1),
            CreatedAt = DateTime.UtcNow,
            IsVerified = false
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1")
        }));
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(1);
        result.Value.Email.Should().Be("test@test.com");
        result.Value.DisplayName.Should().Be("Test User");
        result.Value.Bio.Should().Be("Test bio");
        result.Value.Gender.Should().Be(Gender.Male);
        result.Value.BirthDate.Should().Be(new DateTime(1990, 1, 1));
        result.Value.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_Return_Correct_Statistics()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "test@test.com",
            UserName = "testuser",
            DisplayName = "Test User",
            CreatedAt = DateTime.UtcNow
        };

        var category1 = new Category { Id = 1, Type = Shared.Enums.CategoryType.Sports };
        var category2 = new Category { Id = 2, Type = Shared.Enums.CategoryType.Food };

        var event1 = new Event
        {
            Id = 1,
            Title = "Event1",
            CreatedByUserId = 1,
            CategoryId = 1,
            DateTime = DateTime.UtcNow,
            MinAttendance = 1,
            MaxAttendance = 10
        };
        var event2 = new Event
        {
            Id = 2,
            Title = "Event2",
            CreatedByUserId = 1,
            CategoryId = 1,
            DateTime = DateTime.UtcNow,
            MinAttendance = 1,
            MaxAttendance = 10
        };

        await _context.Users.AddAsync(user);
        await _context.Categories.AddRangeAsync(category1, category2);
        await _context.Events.AddRangeAsync(event1, event2);

        await _context.UserCategories.AddRangeAsync(
            new UserCategory { UserId = 1, CategoryId = 1 },
            new UserCategory { UserId = 1, CategoryId = 2 }
        );

        await _context.EventParticipants.AddAsync(
            new EventParticipant { EventId = 1, UserId = 1 }
        );

        await _context.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1")
        }));
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.EventsCreated.Should().Be(2);
        result.Value.EventsJoined.Should().Be(1);
        result.Value.CategoriesCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_Should_Return_Zero_Statistics_For_New_User()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Email = "new@test.com",
            UserName = "newuser",
            DisplayName = "New User",
            CreatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1")
        }));
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = await _handler.Handle();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.EventsCreated.Should().Be(0);
        result.Value.EventsJoined.Should().Be(0);
        result.Value.CategoriesCount.Should().Be(0);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
