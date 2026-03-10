namespace Shared.Contracts.Events;

public class LeaveEventResponse
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public DateTime LeftAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
