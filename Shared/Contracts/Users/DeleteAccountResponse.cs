namespace Shared.Contracts.Users;

public class DeleteAccountResponse
{
    public string MessageKey { get; set; } = string.Empty;
    public int CancelledEventsCount { get; set; }
    public int LeftEventsCount { get; set; }
    public int EmailsSentCount { get; set; }
}
