namespace Shared.Contracts.Events;

public class JoinEventResponse
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
