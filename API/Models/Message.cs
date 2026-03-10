namespace API.Models;

public class Message
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }

    // Moderation fields
    public bool IsFlagged { get; set; }
    public int? ModerationSeverity { get; set; }
    public string? ModerationCategory { get; set; }
    
    // Soft delete for moderation
    public bool IsDeletedByModeration { get; set; }
    public DateTime? DeletedByModerationAt { get; set; }
}
