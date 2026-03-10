using Shared.Enums;

namespace Shared.Contracts.Events;

public class GetEventDetailsResponse
{
    public int Id { get; set; }
    public string EventHash { get; set; } = string.Empty; // Hash for use in URLs
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime DateTime { get; set; }
    public DateTime CreatedAt { get; set; }

    public int CreatedByUserId { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public CategoryType Category { get; set; }

    public int MinAttendance { get; set; }
    public int MaxAttendance { get; set; }
    public int CurrentAttendance { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public GenderRestriction GenderRestriction { get; set; }
    public EventStatus Status { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsPublic { get; set; }
    public bool OnlyVerifiedUsers { get; set; }

    public bool IsUserParticipant { get; set; }
    public bool IsUserCreator { get; set; }

    public List<EventParticipantDto> Participants { get; set; } = new();
    public List<EventParticipantDto> BlockedParticipants { get; set; } = new();
}
