using API.Models;
using FluentAssertions;
using Shared.Enums;
using Xunit;

namespace API.Tests.Unit.Models;

/// <summary>
/// Unit tests for EventParticipant model.
/// Tests role assignment, status, and relationships.
/// </summary>
public class EventParticipantTests
{
    [Fact]
    public void EventParticipant_Should_Have_Default_Participant_Role()
    {
        // Arrange & Act
        var participant = new EventParticipant
        {
            EventId = 1,
            UserId = 1,
            JoinedAt = DateTime.UtcNow,
            Status = ParticipantStatus.Accepted
        };

        // Assert
        participant.Role.Should().Be(EventRole.Participant);
    }

    [Fact]
    public void EventParticipant_Should_Allow_Creator_Role()
    {
        // Arrange & Act
        var creator = new EventParticipant
        {
            EventId = 1,
            UserId = 1,
            JoinedAt = DateTime.UtcNow,
            Status = ParticipantStatus.Accepted,
            Role = EventRole.Creator
        };

        // Assert
        creator.Role.Should().Be(EventRole.Creator);
    }

    [Fact]
    public void EventParticipant_Should_Allow_CoOrganizer_Role()
    {
        // Arrange & Act
        var coOrganizer = new EventParticipant
        {
            EventId = 1,
            UserId = 1,
            JoinedAt = DateTime.UtcNow,
            Status = ParticipantStatus.Accepted,
            Role = EventRole.CoOrganizer
        };

        // Assert
        coOrganizer.Role.Should().Be(EventRole.CoOrganizer);
    }

    [Fact]
    public void EventParticipant_Should_Have_Default_Accepted_Status()
    {
        // Arrange & Act
        var participant = new EventParticipant
        {
            EventId = 1,
            UserId = 1,
            JoinedAt = DateTime.UtcNow
        };

        // Assert
        participant.Status.Should().Be(ParticipantStatus.Accepted);
    }

    [Theory]
    [InlineData(EventRole.Creator)]
    [InlineData(EventRole.Participant)]
    [InlineData(EventRole.CoOrganizer)]
    public void EventParticipant_Should_Support_All_Role_Types(EventRole role)
    {
        // Arrange & Act
        var participant = new EventParticipant
        {
            EventId = 1,
            UserId = 1,
            JoinedAt = DateTime.UtcNow,
            Status = ParticipantStatus.Accepted,
            Role = role
        };

        // Assert
        participant.Role.Should().Be(role);
    }

    [Theory]
    [InlineData(ParticipantStatus.Accepted)]
    [InlineData(ParticipantStatus.Pending)]
    [InlineData(ParticipantStatus.Declined)]
    [InlineData(ParticipantStatus.Removed)]
    public void EventParticipant_Should_Support_All_Status_Types(ParticipantStatus status)
    {
        // Arrange & Act
        var participant = new EventParticipant
        {
            EventId = 1,
            UserId = 1,
            JoinedAt = DateTime.UtcNow,
            Status = status
        };

        // Assert
        participant.Status.Should().Be(status);
    }

    [Fact]
    public void EventParticipant_Should_Have_Required_EventId()
    {
        // Arrange & Act
        var participant = new EventParticipant
        {
            EventId = 42,
            UserId = 1,
            JoinedAt = DateTime.UtcNow
        };

        // Assert
        participant.EventId.Should().Be(42);
    }

    [Fact]
    public void EventParticipant_Should_Have_Required_UserId()
    {
        // Arrange & Act
        var participant = new EventParticipant
        {
            EventId = 1,
            UserId = 99,
            JoinedAt = DateTime.UtcNow
        };

        // Assert
        participant.UserId.Should().Be(99);
    }

    [Fact]
    public void EventParticipant_Should_Store_JoinedAt_DateTime()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var participant = new EventParticipant
        {
            EventId = 1,
            UserId = 1,
            JoinedAt = now
        };

        // Assert
        participant.JoinedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void EventParticipant_Creator_Should_Be_Accepted()
    {
        // Arrange & Act
        var creator = new EventParticipant
        {
            EventId = 1,
            UserId = 1,
            JoinedAt = DateTime.UtcNow,
            Status = ParticipantStatus.Accepted,
            Role = EventRole.Creator
        };

        // Assert
        creator.Role.Should().Be(EventRole.Creator);
        creator.Status.Should().Be(ParticipantStatus.Accepted);
    }

    [Fact]
    public void EventParticipant_CoOrganizer_Should_Be_Accepted()
    {
        // Arrange & Act
        var coOrganizer = new EventParticipant
        {
            EventId = 1,
            UserId = 1,
            JoinedAt = DateTime.UtcNow,
            Status = ParticipantStatus.Accepted,
            Role = EventRole.CoOrganizer
        };

        // Assert
        coOrganizer.Role.Should().Be(EventRole.CoOrganizer);
        coOrganizer.Status.Should().Be(ParticipantStatus.Accepted);
    }

    [Fact]
    public void EventParticipant_Regular_Participant_Can_Have_Different_Statuses()
    {
        // Arrange & Act
        var pendingParticipant = new EventParticipant
        {
            EventId = 1,
            UserId = 1,
            JoinedAt = DateTime.UtcNow,
            Status = ParticipantStatus.Pending,
            Role = EventRole.Participant
        };

        // Assert
        pendingParticipant.Role.Should().Be(EventRole.Participant);
        pendingParticipant.Status.Should().Be(ParticipantStatus.Pending);
    }
}
