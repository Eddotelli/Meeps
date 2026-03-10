using Shared.Enums;

namespace Shared.Contracts.Events;

public class EventParticipantDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public int? Age { get; set; }
    public Gender? Gender { get; set; }
    public bool IsVerified { get; set; }
    public ParticipantStatus Status { get; set; }
    public EventRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? BlockedAt { get; set; }
    public string? BlockedReason { get; set; }

    // Computed property to check if participant is blocked
    public bool IsBlocked => BlockedAt.HasValue;
}
