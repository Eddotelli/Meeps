using Shared.Enums;

namespace API.Models;

public class EventParticipant
{
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime JoinedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ParticipantStatus Status { get; set; } = ParticipantStatus.Accepted;
    public EventRole Role { get; set; } = EventRole.Participant;

    public DateTime? BlockedAt { get; set; }
    public string? BlockedReason { get; set; }
}
