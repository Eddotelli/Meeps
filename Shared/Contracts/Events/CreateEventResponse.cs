namespace Shared.Contracts.Events;

public class CreateEventResponse
{
    public int EventId { get; set; }
    public string EventHash { get; set; } = string.Empty; // Hash for use in URLs
    public string Title { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
    public string Message { get; set; } = string.Empty;
}
