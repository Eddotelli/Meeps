using System.Net;
using System.Net.Http.Json;
using API.Infrastructure.Data;
using API.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Auth;
using Shared.Contracts.Events;
using Shared.Enums;
using Xunit;

namespace API.Tests.Integration;

/// <summary>
/// Integration tests for Events endpoints.
/// Tests critical functionality for event creation.
/// </summary>
public class EventEndpointsTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public EventEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
    }

    public async Task InitializeAsync()
    {
        // Seed test data: Create categories
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Add test categories if they don't exist
        if (!context.Categories.Any())
        {
            context.Categories.AddRange(
                new Category { Id = 1, Type = CategoryType.Sports },
                new Category { Id = 2, Type = CategoryType.Music },
                new Category { Id = 3, Type = CategoryType.Technology }
            );
            await context.SaveChangesAsync();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateEvent_Should_Return_Unauthorized_Without_Authentication()
    {
        // Arrange
        var request = CreateValidEventRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/events", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateEvent_Should_Return_Created_With_Valid_Data()
    {
        // Arrange
        await AuthenticateAsync();
        var request = CreateValidEventRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/events", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateEventResponse>();
        result.Should().NotBeNull();
        result!.EventId.Should().BeGreaterThan(0);
        result.Title.Should().Be(request.Title);
    }

    [Fact]
    public async Task CreateEvent_Should_Return_BadRequest_When_Title_Is_Invalid()
    {
        // Arrange
        await AuthenticateAsync();
        var request = CreateValidEventRequest();
        request.Title = "AB"; // Too short

        // Act
        var response = await _client.PostAsJsonAsync("/api/events", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_Should_Return_BadRequest_When_DateTime_Is_In_Past()
    {
        // Arrange
        await AuthenticateAsync();
        var request = CreateValidEventRequest();
        request.DateTime = DateTime.UtcNow.AddHours(-1);

        // Act
        var response = await _client.PostAsJsonAsync("/api/events", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_Should_Return_BadRequest_When_Attendance_Range_Is_Invalid()
    {
        // Arrange
        await AuthenticateAsync();
        var request = CreateValidEventRequest();
        request.MinAttendance = 20;
        request.MaxAttendance = 10;

        // Act
        var response = await _client.PostAsJsonAsync("/api/events", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_Should_Return_NotFound_When_Category_Does_Not_Exist()
    {
        // Arrange
        await AuthenticateAsync();
        var request = CreateValidEventRequest();
        request.CategoryId = 9999;

        // Act
        var response = await _client.PostAsJsonAsync("/api/events", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #region LeaveEvent Tests

    [Fact]
    public async Task LeaveEvent_Should_Return_Unauthorized_Without_Authentication()
    {
        // Arrange
        var eventHash = "test-hash-123"; // Doesn't matter, should fail auth first

        // Act
        var response = await _client.PostAsJsonAsync($"/api/events/{eventHash}/leave", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LeaveEvent_When_UserIsParticipant_Should_Return_Success()
    {
        // Arrange - Create two users: one to create event, one to join and leave
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();

        // Logout and create second user
        await _client.PostAsync("/api/auth/logout", null);
        await AuthenticateAsync(); // Creates a new user

        // Second user joins the event
        await _client.PostAsJsonAsync($"/api/events/{eventHash}/join", new { });

        // Act - Second user leaves the event
        var response = await _client.PostAsJsonAsync($"/api/events/{eventHash}/leave", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LeaveEventResponse>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task LeaveEvent_When_UserNotParticipant_Should_Return_Conflict()
    {
        // Arrange - Create two users: one to create event, one that doesn't join
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();

        // Logout and create second user
        await _client.PostAsync("/api/auth/logout", null);
        await AuthenticateAsync(); // Creates a new user

        // Act - Second user tries to leave without joining first
        var response = await _client.PostAsJsonAsync($"/api/events/{eventHash}/leave", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task LeaveEvent_When_UserIsCreator_Should_Return_BadRequest()
    {
        // Arrange
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();

        // Act - Creator tries to leave their own event (creator is automatically a participant)
        var response = await _client.PostAsJsonAsync($"/api/events/{eventHash}/leave", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LeaveEvent_When_EventNotFound_Should_Return_NotFound()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.PostAsJsonAsync("/api/events/9999/leave", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LeaveEvent_Should_Update_AttendanceCount()
    {
        // Arrange - Create two users
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();

        // Logout and create second user
        await _client.PostAsync("/api/auth/logout", null);
        await AuthenticateAsync(); // Creates a new user

        // Second user joins the event
        await _client.PostAsJsonAsync($"/api/events/{eventHash}/join", new { });

        // Get event details to check attendance before leaving
        var beforeResponse = await _client.GetAsync($"/api/events/{eventHash}");
        var beforeDetails = await beforeResponse.Content.ReadFromJsonAsync<GetEventDetailsResponse>();
        var attendanceBefore = beforeDetails!.CurrentAttendance;

        // Act - Second user leaves the event
        await _client.PostAsJsonAsync($"/api/events/{eventHash}/leave", new { });

        // Assert - Check attendance decreased
        var afterResponse = await _client.GetAsync($"/api/events/{eventHash}");
        var afterDetails = await afterResponse.Content.ReadFromJsonAsync<GetEventDetailsResponse>();
        afterDetails!.CurrentAttendance.Should().Be(attendanceBefore - 1);
    }

    #endregion

    #region UpdateEvent Tests

    [Fact]
    public async Task UpdateEvent_Should_Return_Unauthorized_Without_Authentication()
    {
        // Arrange
        var request = CreateValidUpdateEventRequest("test-hash-123");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/events/{request.EventHash}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateEvent_Should_Return_Success_When_User_Is_Creator()
    {
        // Arrange
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();
        var request = CreateValidUpdateEventRequest(eventHash);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/events/{eventHash}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UpdateEventResponse>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateEvent_Should_Return_BadRequest_When_Title_Is_Invalid()
    {
        // Arrange
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();
        var request = CreateValidUpdateEventRequest(eventHash);
        request.Title = "AB"; // Too short

        // Act
        var response = await _client.PutAsJsonAsync($"/api/events/{eventHash}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateEvent_Should_Return_Forbidden_When_User_Is_Not_Creator()
    {
        // Arrange - Create event with first user
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();

        // Logout and authenticate as second user
        await _client.PostAsync("/api/auth/logout", null);
        await AuthenticateAsync();

        var request = CreateValidUpdateEventRequest(eventHash);

        // Act - Second user tries to update first user's event
        var response = await _client.PutAsJsonAsync($"/api/events/{eventHash}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateEvent_Should_Return_NotFound_When_Event_Does_Not_Exist()
    {
        // Arrange
        await AuthenticateAsync();
        var request = CreateValidUpdateEventRequest("invalid-hash-9999");

        // Act
        var response = await _client.PutAsJsonAsync("/api/events/invalid-hash-9999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateEvent_Should_Return_NotFound_When_Category_Does_Not_Exist()
    {
        // Arrange
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();
        var request = CreateValidUpdateEventRequest(eventHash);
        request.CategoryId = 9999;

        // Act
        var response = await _client.PutAsJsonAsync($"/api/events/{eventHash}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region JoinEvent Tests

    [Fact]
    public async Task JoinEvent_Should_Return_Unauthorized_Without_Authentication()
    {
        // Arrange
        var eventHash = "test-hash-123"; // Doesn't matter, should fail auth first

        // Act
        var response = await _client.PostAsJsonAsync($"/api/events/{eventHash}/join", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JoinEvent_Should_Return_Success_When_Valid()
    {
        // Arrange - Create event with first user
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();

        // Logout and authenticate as second user
        await _client.PostAsync("/api/auth/logout", null);
        await AuthenticateAsync();

        // Act - Second user joins the event
        var response = await _client.PostAsJsonAsync($"/api/events/{eventHash}/join", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JoinEventResponse>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task JoinEvent_Should_Return_Conflict_When_Already_Participant()
    {
        // Arrange - Create event with first user
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();

        // Logout and authenticate as second user
        await _client.PostAsync("/api/auth/logout", null);
        await AuthenticateAsync();

        // Join once
        await _client.PostAsJsonAsync($"/api/events/{eventHash}/join", new { });

        // Act - Try to join again
        var response = await _client.PostAsJsonAsync($"/api/events/{eventHash}/join", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task JoinEvent_Should_Return_NotFound_When_Event_Does_Not_Exist()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.PostAsJsonAsync("/api/events/9999/join", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task JoinEvent_Should_Increase_Attendance_Count()
    {
        // Arrange - Create event with first user
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();

        // Get initial attendance
        var beforeResponse = await _client.GetAsync($"/api/events/{eventHash}");
        var beforeDetails = await beforeResponse.Content.ReadFromJsonAsync<GetEventDetailsResponse>();
        var attendanceBefore = beforeDetails!.CurrentAttendance;

        // Logout and authenticate as second user
        await _client.PostAsync("/api/auth/logout", null);
        await AuthenticateAsync();

        // Act - Second user joins
        await _client.PostAsJsonAsync($"/api/events/{eventHash}/join", new { });

        // Assert - Check attendance increased
        var afterResponse = await _client.GetAsync($"/api/events/{eventHash}");
        var afterDetails = await afterResponse.Content.ReadFromJsonAsync<GetEventDetailsResponse>();
        afterDetails!.CurrentAttendance.Should().Be(attendanceBefore + 1);
    }

    #endregion

    #region GetEventDetails Tests

    [Fact]
    public async Task GetEventDetails_Should_Return_Success_For_Public_Event()
    {
        // Arrange
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();

        // Act
        var response = await _client.GetAsync($"/api/events/{eventHash}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetEventDetailsResponse>();
        result.Should().NotBeNull();
        result!.IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task GetEventDetails_Should_Return_NotFound_When_Event_Does_Not_Exist()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.GetAsync("/api/events/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetEventDetails_Should_Show_IsUserCreator_For_Creator()
    {
        // Arrange
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();

        // Act
        var response = await _client.GetAsync($"/api/events/{eventHash}");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<GetEventDetailsResponse>();
        result.Should().NotBeNull();
        result!.IsUserCreator.Should().BeTrue();
    }

    [Fact]
    public async Task GetEventDetails_Should_Show_IsUserParticipant_For_Participant()
    {
        // Arrange - Create event with first user
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();

        // Logout and authenticate as second user
        await _client.PostAsync("/api/auth/logout", null);
        await AuthenticateAsync();

        // Join event
        await _client.PostAsJsonAsync($"/api/events/{eventHash}/join", new { });

        // Act
        var response = await _client.GetAsync($"/api/events/{eventHash}");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<GetEventDetailsResponse>();
        result.Should().NotBeNull();
        result!.IsUserParticipant.Should().BeTrue();
        result.IsUserCreator.Should().BeFalse();
    }

    [Fact]
    public async Task GetEventDetails_Should_Return_CurrentAttendance()
    {
        // Arrange
        await AuthenticateAsync();
        var eventHash = await CreateTestEventAsync();

        // Act
        var response = await _client.GetAsync($"/api/events/{eventHash}");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<GetEventDetailsResponse>();
        result.Should().NotBeNull();
        result!.CurrentAttendance.Should().BeGreaterThan(0); // Creator is automatically a participant
    }

    #endregion

    private async Task<string> CreateTestEventAsync()
    {
        var request = CreateValidEventRequest();
        var response = await _client.PostAsJsonAsync("/api/events", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateEventResponse>();
        return result!.EventHash; // Return EventHash instead of EventId for URL usage
    }

    private async Task AuthenticateAsync()
    {
        // Register and complete registration for a test user
        var uniqueEmail = $"test.{Guid.NewGuid()}@example.com";
        var uniqueDisplayName = $"TestUser{Guid.NewGuid().ToString()[..8]}";

        // Step 1: Register
        var registerRequest = new RegisterRequest { Email = uniqueEmail };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        // Step 2: Get verification token from test endpoint
        var tokenResponse = await _client.GetAsync($"/api/test/verification-token/{Uri.EscapeDataString(uniqueEmail)}");
        tokenResponse.EnsureSuccessStatusCode();
        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var verificationToken = tokenData!["token"];

        // Step 3: Verify email
        var verifyRequest = new VerifyEmailRequest(verificationToken);
        var verifyResponse = await _client.PostAsJsonAsync("/api/auth/verify-email", verifyRequest);
        verifyResponse.EnsureSuccessStatusCode();

        // Step 4: Complete registration
        var completeRequest = new CompleteRegistrationRequest
        {
            VerificationToken = verificationToken,
            Password = "TestPassword123!",
            DisplayName = uniqueDisplayName,
            BirthDate = new DateTime(1990, 1, 1),
            AcceptTerms = true
        };

        var completeResponse = await _client.PostAsJsonAsync("/api/auth/complete-registration", completeRequest);
        completeResponse.EnsureSuccessStatusCode();

        // Step 5: Login to get authentication token
        var loginRequest = new LoginRequest
        {
            Email = uniqueEmail,
            Password = "TestPassword123!"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();
    }

    private static CreateEventRequest CreateValidEventRequest()
    {
        return new CreateEventRequest
        {
            Title = $"Test Event {Guid.NewGuid().ToString()[..8]}",
            Description = "This is a test event created by integration tests to verify functionality.",
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

    private static UpdateEventRequest CreateValidUpdateEventRequest(string eventHash)
    {
        return new UpdateEventRequest
        {
            EventHash = eventHash,
            Title = $"Updated Event {Guid.NewGuid().ToString()[..8]}",
            Description = "This is an updated test event description for integration tests.",
            Location = "Gothenburg, Sweden",
            Latitude = 57.7089,
            Longitude = 11.9746,
            DateTime = DateTime.UtcNow.AddDays(10),
            CategoryId = 1,
            MinAttendance = 3,
            MaxAttendance = 15,
            MinAge = 21,
            MaxAge = 65,
            GenderRestriction = GenderRestriction.None,
            ImageUrl = "https://example.com/updated-image.jpg",
            IsPublic = true
        };
    }

    #region GetEligibleEvents Tests

    [Fact]
    public async Task GetEligibleEvents_Should_Return_Success_With_Valid_Parameters()
    {
        // Arrange
        await AuthenticateAsync();

        // Create an eligible event
        var createRequest = CreateValidEventRequest();
        createRequest.Latitude = 59.3293;
        createRequest.Longitude = 18.0686;
        var createResponse = await _client.PostAsJsonAsync("/api/events", createRequest);
        createResponse.EnsureSuccessStatusCode();

        // Act
        var response = await _client.GetAsync("/api/events/eligible?latitude=59.3293&longitude=18.0686&radiusKm=50&pageNumber=1&pageSize=20&sortBy=distance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEligibleEvents_Should_Return_Success_Without_Location()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.GetAsync("/api/events/eligible?pageNumber=1&pageSize=20&sortBy=date");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEligibleEvents_Should_Return_BadRequest_When_PageNumber_Is_Zero()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.GetAsync("/api/events/eligible?pageNumber=0&pageSize=20&sortBy=date");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEligibleEvents_Should_Return_BadRequest_When_PageSize_Exceeds_Maximum()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.GetAsync("/api/events/eligible?pageNumber=1&pageSize=150&sortBy=date");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEligibleEvents_Should_Return_BadRequest_When_SortBy_Is_Invalid()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.GetAsync("/api/events/eligible?pageNumber=1&pageSize=20&sortBy=invalid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEligibleEvents_Should_Return_BadRequest_When_Latitude_Without_Longitude()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.GetAsync("/api/events/eligible?latitude=59.3293&pageNumber=1&pageSize=20&sortBy=distance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEligibleEvents_Should_Return_BadRequest_When_Longitude_Without_Latitude()
    {
        // Arrange
        await AuthenticateAsync();

        // Act
        var response = await _client.GetAsync("/api/events/eligible?longitude=18.0686&pageNumber=1&pageSize=20&sortBy=distance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEligibleEvents_Should_Return_Unauthorized_When_Not_Authenticated()
    {
        // Act
        var response = await _client.GetAsync("/api/events/eligible?pageNumber=1&pageSize=20&sortBy=date");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEligibleEvents_Should_Filter_By_CategoryId()
    {
        // Arrange
        await AuthenticateAsync();

        // Create an event with category 1
        var createRequest = CreateValidEventRequest();
        createRequest.CategoryId = 1;
        var createResponse = await _client.PostAsJsonAsync("/api/events", createRequest);
        createResponse.EnsureSuccessStatusCode();

        // Act
        var response = await _client.GetAsync("/api/events/eligible?categoryId=1&pageNumber=1&pageSize=20&sortBy=date");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}
