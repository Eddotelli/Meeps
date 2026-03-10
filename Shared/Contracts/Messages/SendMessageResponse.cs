namespace Shared.Contracts.Messages;

public class SendMessageResponse
{
    public int MessageId { get; set; }
    public int EventId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsAuthorStillParticipant { get; set; }

    // Moderation fields
    public bool WasBlocked { get; set; }
    public bool WasFlagged { get; set; }
    public string? ModerationWarning { get; set; }
    public int? Severity { get; set; }
    public string? Category { get; set; }
}
