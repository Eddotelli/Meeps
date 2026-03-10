using API.Features.Events.CreateEvent;
using API.Infrastructure.Data;
using API.Models;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shared.Contracts.Events;
using Shared.Enums;
using System.Security.Claims;
using Xunit;

namespace API.Tests.Features.Events.CreateEvent;

/// <summary>
/// Unit tests for CreateEventValidator.
/// Tests critical validation rules for event creation.
/// </summary>
public class CreateEventValidatorTests
{
    private readonly CreateEventValidator _validator;
    private readonly ApplicationDbContext _context;
    private const int TestUserId = 1;

    public CreateEventValidatorTests()
    {
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // Add test user to database
        var testUser = new User
        {
            Id = TestUserId,
            Email = "test@example.com",
            DisplayName = "Test User",
            Gender = Gender.Male,
            BirthDate = new DateTime(1990, 1, 1),
            CreatedAt = DateTime.UtcNow,
            IsVerified = true
        };
        _context.Users.Add(testUser);
        _context.SaveChanges();

        // Create mock for IHttpContextAccessor with user claims
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = claimsPrincipal };
        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        _validator = new CreateEventValidator(_context, mockHttpContextAccessor.Object);
    }

    [Fact]
    public async Task Should_Not_Have_Any_Errors_When_Request_Is_Valid()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Should_Have_Error_When_Title_Is_Too_Short()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Title = "AB";

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public async Task Should_Have_Error_When_Title_Is_Too_Long()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Title = new string('A', 101);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public async Task Should_Have_Error_When_Description_Is_Too_Short()
    {
        // Arrange
        var request = CreateValidRequest();
        request.Description = "Short";

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public async Task Should_Have_Error_When_DateTime_Is_In_Past()
    {
        // Arrange
        var request = CreateValidRequest();
        request.DateTime = DateTime.UtcNow.AddHours(-1);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DateTime);
    }

    [Fact]
    public async Task Should_Have_Error_When_CategoryId_Is_Invalid()
    {
        // Arrange
        var request = CreateValidRequest();
        request.CategoryId = 0;

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CategoryId);
    }

    [Fact]
    public async Task Should_Have_Error_When_MaxAttendance_Is_Less_Than_MinAttendance()
    {
        // Arrange
        var request = CreateValidRequest();
        request.MinAttendance = 10;
        request.MaxAttendance = 5;

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxAttendance);
    }

    [Fact]
    public async Task Should_Have_Error_When_MinAge_Is_Below_Minimum()
    {
        // Arrange
        var request = CreateValidRequest();
        request.MinAge = 0;

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MinAge);
    }

    [Fact]
    public async Task Should_Have_Error_When_MaxAge_Is_Less_Than_MinAge()
    {
        // Arrange
        var request = CreateValidRequest();
        request.MinAge = 30;
        request.MaxAge = 18;

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxAge);
    }

    [Fact]
    public async Task Should_Not_Have_Error_When_ImageUrl_Is_Null()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ImageUrl = null;

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ImageUrl);
    }

    [Fact]
    public async Task Should_Not_Have_Error_When_ImageUrl_Is_Valid_Http()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ImageUrl = "http://example.com/image.jpg";

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ImageUrl);
    }

    [Fact]
    public async Task Should_Not_Have_Error_When_ImageUrl_Is_Valid_Https()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ImageUrl = "https://example.com/image.jpg";

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ImageUrl);
    }

    [Fact]
    public async Task Should_Have_Error_When_ImageUrl_Is_Invalid()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ImageUrl = "not-a-valid-url";

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ImageUrl);
    }

    [Fact]
    public async Task Should_Have_Error_When_ImageUrl_Is_Too_Long()
    {
        // Arrange
        var request = CreateValidRequest();
        request.ImageUrl = "https://example.com/" + new string('a', 500);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ImageUrl);
    }

    private static CreateEventRequest CreateValidRequest()
    {
        return new CreateEventRequest
        {
            Title = "Test Event",
            Description = "This is a valid test event description.",
            Location = "Stockholm, Sweden",
            DateTime = DateTime.UtcNow.AddDays(7),
            CategoryId = 1,
            MinAttendance = 5,
            MaxAttendance = 20,
            MinAge = 18,
            MaxAge = 99,
            GenderRestriction = GenderRestriction.None,
            IsPublic = true
        };
    }
}
