using API.Infrastructure.Data;
using API.Infrastructure.Services;
using API.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;
using Xunit;

namespace API.Tests.Infrastructure.Services;

/// <summary>
/// Unit tests for EventPermissionsService.
/// Tests event role-based permission checks.
/// </summary>
public class EventPermissionsServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly EventPermissionsService _service;
    private const int EventId = 1;
    private const int CreatorUserId = 1;
    private const int CoOrganizerUserId = 2;
    private const int ParticipantUserId = 3;
    private const int PendingUserId = 4;
    private const int NonParticipantUserId = 99;

    public EventPermissionsServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _service = new EventPermissionsService(_context);

        SeedTestData();
    }

    private void SeedTestData()
    {
        // Add test event participants with different roles and statuses
        _context.EventParticipants.AddRange(
            new EventParticipant
            {
                EventId = EventId,
                UserId = CreatorUserId,
                Role = EventRole.Creator,
                Status = ParticipantStatus.Accepted,
                JoinedAt = DateTime.UtcNow
            },
            new EventParticipant
            {
                EventId = EventId,
                UserId = CoOrganizerUserId,
                Role = EventRole.CoOrganizer,
                Status = ParticipantStatus.Accepted,
                JoinedAt = DateTime.UtcNow
            },
            new EventParticipant
            {
                EventId = EventId,
                UserId = ParticipantUserId,
                Role = EventRole.Participant,
                Status = ParticipantStatus.Accepted,
                JoinedAt = DateTime.UtcNow
            },
            new EventParticipant
            {
                EventId = EventId,
                UserId = PendingUserId,
                Role = EventRole.Participant,
                Status = ParticipantStatus.Pending,
                JoinedAt = DateTime.UtcNow
            }
        );

        _context.SaveChanges();
    }

    #region CanEditEvent Tests

    [Fact]
    public async Task CanEditEvent_Should_Return_True_When_User_Is_Creator()
    {
        // Arrange & Act
        var result = await _service.CanEditEvent(CreatorUserId, EventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanEditEvent_Should_Return_True_When_User_Is_CoOrganizer()
    {
        // Arrange & Act
        var result = await _service.CanEditEvent(CoOrganizerUserId, EventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanEditEvent_Should_Return_False_When_User_Is_Participant()
    {
        // Arrange & Act
        var result = await _service.CanEditEvent(ParticipantUserId, EventId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanEditEvent_Should_Return_False_When_User_Not_Participant()
    {
        // Arrange & Act
        var result = await _service.CanEditEvent(NonParticipantUserId, EventId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CanDeleteEvent Tests

    [Fact]
    public async Task CanDeleteEvent_Should_Return_True_When_User_Is_Creator()
    {
        // Arrange & Act
        var result = await _service.CanDeleteEvent(CreatorUserId, EventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanDeleteEvent_Should_Return_False_When_User_Is_CoOrganizer()
    {
        // Arrange & Act
        var result = await _service.CanDeleteEvent(CoOrganizerUserId, EventId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanDeleteEvent_Should_Return_False_When_User_Is_Participant()
    {
        // Arrange & Act
        var result = await _service.CanDeleteEvent(ParticipantUserId, EventId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanDeleteEvent_Should_Return_False_When_User_Not_Participant()
    {
        // Arrange & Act
        var result = await _service.CanDeleteEvent(NonParticipantUserId, EventId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CanInviteUsers Tests

    [Fact]
    public async Task CanInviteUsers_Should_Return_True_When_User_Is_Creator()
    {
        // Arrange & Act
        var result = await _service.CanInviteUsers(CreatorUserId, EventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanInviteUsers_Should_Return_True_When_User_Is_CoOrganizer()
    {
        // Arrange & Act
        var result = await _service.CanInviteUsers(CoOrganizerUserId, EventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanInviteUsers_Should_Return_False_When_User_Is_Participant()
    {
        // Arrange & Act
        var result = await _service.CanInviteUsers(ParticipantUserId, EventId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanInviteUsers_Should_Return_False_When_User_Not_Participant()
    {
        // Arrange & Act
        var result = await _service.CanInviteUsers(NonParticipantUserId, EventId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CanManageParticipants Tests

    [Fact]
    public async Task CanManageParticipants_Should_Return_True_When_User_Is_Creator()
    {
        // Arrange & Act
        var result = await _service.CanManageParticipants(CreatorUserId, EventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanManageParticipants_Should_Return_True_When_User_Is_CoOrganizer()
    {
        // Arrange & Act
        var result = await _service.CanManageParticipants(CoOrganizerUserId, EventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanManageParticipants_Should_Return_False_When_User_Is_Participant()
    {
        // Arrange & Act
        var result = await _service.CanManageParticipants(ParticipantUserId, EventId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanManageParticipants_Should_Return_False_When_User_Not_Participant()
    {
        // Arrange & Act
        var result = await _service.CanManageParticipants(NonParticipantUserId, EventId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CanAccessChat Tests

    [Fact]
    public async Task CanAccessChat_Should_Return_True_When_User_Is_Accepted_Creator()
    {
        // Arrange & Act
        var result = await _service.CanAccessChat(CreatorUserId, EventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessChat_Should_Return_True_When_User_Is_Accepted_CoOrganizer()
    {
        // Arrange & Act
        var result = await _service.CanAccessChat(CoOrganizerUserId, EventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessChat_Should_Return_True_When_User_Is_Accepted_Participant()
    {
        // Arrange & Act
        var result = await _service.CanAccessChat(ParticipantUserId, EventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessChat_Should_Return_False_When_User_Status_Is_Pending()
    {
        // Arrange & Act
        var result = await _service.CanAccessChat(PendingUserId, EventId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessChat_Should_Return_False_When_User_Not_Participant()
    {
        // Arrange & Act
        var result = await _service.CanAccessChat(NonParticipantUserId, EventId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessChat_Should_Return_False_When_User_Status_Is_Declined()
    {
        // Arrange
        _context.EventParticipants.Add(new EventParticipant
        {
            EventId = EventId,
            UserId = 5,
            Role = EventRole.Participant,
            Status = ParticipantStatus.Declined,
            JoinedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CanAccessChat(5, EventId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
