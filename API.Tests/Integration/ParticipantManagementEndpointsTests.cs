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
/// Fixture for ParticipantManagement tests - ensures test data is created once per test class.
/// </summary>
public class ParticipantManagementTestFixture : IAsyncLifetime
{
    public CustomWebApplicationFactory Factory { get; private set; }
    public int TestEventId { get; private set; }
    public int CreatorUserId { get; private set; }
    public int ParticipantUserId { get; private set; }

    public ParticipantManagementTestFixture()
    {
        Factory = new CustomWebApplicationFactory();
    }

    public async Task InitializeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Create test users
        var creatorUser = new User
        {
            UserName = "creator@test.com",
            NormalizedUserName = "CREATOR@TEST.COM",
            Email = "creator@test.com",
            NormalizedEmail = "CREATOR@TEST.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var participantUser = new User
        {
            UserName = "participant@test.com",
            NormalizedUserName = "PARTICIPANT@TEST.COM",
            Email = "participant@test.com",
            NormalizedEmail = "PARTICIPANT@TEST.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        context.Users.AddRange(creatorUser, participantUser);
        await context.SaveChangesAsync();

        CreatorUserId = creatorUser.Id;
        ParticipantUserId = participantUser.Id;

        // Create test event
        var testEvent = new Event
        {
            Title = "Test Event for Participant Management",
            Description = "Event to test participant blocking/unblocking",
            DateTime = DateTime.UtcNow.AddDays(7),
            Location = "Test Location",
            CategoryId = 1,
            CreatedByUserId = CreatorUserId,
            Status = EventStatus.Active,
            MinAttendance = 2,
            MaxAttendance = 10,
            CreatedAt = DateTime.UtcNow
        };

        context.Events.Add(testEvent);
        await context.SaveChangesAsync();

        TestEventId = testEvent.Id;

        // Add creator as participant with Creator role
        context.EventParticipants.Add(new EventParticipant
        {
            EventId = TestEventId,
            UserId = CreatorUserId,
            Role = EventRole.Creator,
            Status = ParticipantStatus.Accepted,
            JoinedAt = DateTime.UtcNow
        });

        // Add second user as regular participant
        var participant = new EventParticipant
        {
            EventId = TestEventId,
            UserId = ParticipantUserId,
            Role = EventRole.Participant,
            Status = ParticipantStatus.Accepted,
            JoinedAt = DateTime.UtcNow
        };

        context.EventParticipants.Add(participant);
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        Factory?.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Integration tests for Block/Unblock Participant endpoints.
/// Tests permission-based participant management functionality.
/// </summary>
public class ParticipantManagementEndpointsTests : IClassFixture<ParticipantManagementTestFixture>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly ParticipantManagementTestFixture _fixture;

    public ParticipantManagementEndpointsTests(
        ParticipantManagementTestFixture fixture)
    {
        _fixture = fixture;
        _factory = fixture.Factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
    }

    #region Helper Methods

    private async Task AuthenticateAsCreatorAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();

        // Find user by ID
        var user = await userManager.FindByIdAsync(_fixture.CreatorUserId.ToString());
        if (user == null)
            throw new InvalidOperationException("Creator user not found");

        // Set password using password hasher
        user.PasswordHash = passwordHasher.HashPassword(user, "Creator123!");
        await userManager.UpdateAsync(user);

        // Login (cookies will be set automatically)
        var loginRequest = new LoginRequest
        {
            Email = "creator@test.com",
            Password = "Creator123!"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();
    }

    private async Task AuthenticateAsParticipantAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();

        // Find user by ID
        var user = await userManager.FindByIdAsync(_fixture.ParticipantUserId.ToString());
        if (user == null)
            throw new InvalidOperationException("Participant user not found");

        // Set password using password hasher
        user.PasswordHash = passwordHasher.HashPassword(user, "Participant123!");
        await userManager.UpdateAsync(user);

        // Login (cookies will be set automatically)
        var loginRequest = new LoginRequest
        {
            Email = "participant@test.com",
            Password = "Participant123!"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();
    }

    #endregion

    [Fact]
    public async Task BlockParticipant_Should_Return_Unauthorized_Without_Authentication()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/events/{_fixture.TestEventId}/participants/{_fixture.ParticipantUserId}/block",
            new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BlockParticipant_Should_Return_Success_When_Creator_Blocks_Participant()
    {
        // Arrange
        await AuthenticateAsCreatorAsync();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/events/{_fixture.TestEventId}/participants/{_fixture.ParticipantUserId}/block",
            new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BlockParticipantResponse>();
        result.Should().NotBeNull();
        result!.EventId.Should().Be(_fixture.TestEventId);
        result.UserId.Should().Be(_fixture.ParticipantUserId);
        result.Message.Should().Contain("blocked");

        // Verify in database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var participant = await context.EventParticipants
            .FindAsync(_fixture.TestEventId, _fixture.ParticipantUserId);
        participant.Should().NotBeNull();
        participant!.Status.Should().Be(ParticipantStatus.Blocked);
        participant.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task BlockParticipant_Should_Return_Forbidden_When_Regular_User_Tries_To_Block()
    {
        // Arrange
        await AuthenticateAsParticipantAsync();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/events/{_fixture.TestEventId}/participants/{_fixture.CreatorUserId}/block",
            new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BlockParticipant_Should_Return_BadRequest_When_Blocking_Self()
    {
        // Arrange
        await AuthenticateAsCreatorAsync();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/events/{_fixture.TestEventId}/participants/{_fixture.CreatorUserId}/block",
            new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BlockParticipant_Should_Return_BadRequest_When_Already_Blocked()
    {
        // Arrange
        await AuthenticateAsCreatorAsync();
        await _client.PostAsJsonAsync(
            $"/api/events/{_fixture.TestEventId}/participants/{_fixture.ParticipantUserId}/block",
            new { });

        // Act - Try to block again
        var response = await _client.PostAsJsonAsync(
            $"/api/events/{_fixture.TestEventId}/participants/{_fixture.ParticipantUserId}/block",
            new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnblockParticipant_Should_Return_Unauthorized_Without_Authentication()
    {
        // Act
        var response = await _client.DeleteAsync(
            $"/api/events/{_fixture.TestEventId}/participants/{_fixture.ParticipantUserId}/block");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnblockParticipant_Should_Return_Success_When_Creator_Unblocks_Participant()
    {
        // Arrange
        await AuthenticateAsCreatorAsync();

        // Block participant first
        await _client.PostAsJsonAsync(
            $"/api/events/{_fixture.TestEventId}/participants/{_fixture.ParticipantUserId}/block",
            new { });

        // Act - Unblock
        var response = await _client.DeleteAsync(
            $"/api/events/{_fixture.TestEventId}/participants/{_fixture.ParticipantUserId}/block");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UnblockParticipantResponse>();
        result.Should().NotBeNull();
        result!.EventId.Should().Be(_fixture.TestEventId);
        result.UserId.Should().Be(_fixture.ParticipantUserId);
        result.Message.Should().Contain("unblocked");

        // Verify in database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var participant = await context.EventParticipants
            .FindAsync(_fixture.TestEventId, _fixture.ParticipantUserId);
        participant.Should().NotBeNull();
        participant!.Status.Should().Be(ParticipantStatus.Accepted);
    }

    [Fact]
    public async Task UnblockParticipant_Should_Return_BadRequest_When_Not_Blocked()
    {
        // Arrange
        await AuthenticateAsCreatorAsync();

        // Act - Try to unblock participant who is not blocked
        var response = await _client.DeleteAsync(
            $"/api/events/{_fixture.TestEventId}/participants/{_fixture.ParticipantUserId}/block");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnblockParticipant_Should_Return_NotFound_When_Participant_Does_Not_Exist()
    {
        // Arrange
        await AuthenticateAsCreatorAsync();
        var nonExistentUserId = 99999;

        // Act
        var response = await _client.DeleteAsync(
            $"/api/events/{_fixture.TestEventId}/participants/{nonExistentUserId}/block");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
