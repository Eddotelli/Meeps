namespace Shared.Contracts.Events;

public class UpdateEventResponse
{
    public int EventId { get; set; }
    public string Message { get; set; } = string.Empty;
}
