namespace Shared.Contracts.Messages;

public class GetEventMessagesResponse
{
    public List<MessageDto> Messages { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class MessageDto
{
    public int MessageId { get; set; }
    public int EventId { get; set; }
    public string EventHash { get; set; } = string.Empty; // Hash for use in URLs
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsAuthorStillParticipant { get; set; }
    public bool IsUserBlocked { get; set; }
}
