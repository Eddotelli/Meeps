namespace Shared.Contracts.Events;

public class BlockParticipantResponse
{
    public int EventId { get; set; }
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
