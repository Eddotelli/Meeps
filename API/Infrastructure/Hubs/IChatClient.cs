using Shared.Contracts.Messages;

namespace API.Infrastructure.Hubs;

/// <summary>
/// Strongly-typed interface for SignalR client methods
/// This ensures type safety when sending messages to clients
/// </summary>
public interface IChatClient
{
    /// <summary>
    /// Called when a new message is received in an event chat
    /// </summary>
    /// <param name="eventId">The ID of the event</param>
    /// <param name="message">The message DTO</param>
    Task ReceiveMessage(int eventId, MessageDto message);

    /// <summary>
    /// Called when a message is deleted by moderation
    /// </summary>
    Task MessageDeleted(int eventId, int messageId, string reason);

    /// <summary>
    /// Called when a message is flagged with a warning
    /// </summary>
    Task MessageFlagged(int eventId, int messageId, int userId, string warning);

    /// <summary>
    /// Called when a user joins/leaves the event chat (optional for future)
    /// </summary>
    Task UserJoined(int eventId, string userName);

    Task UserLeft(int eventId, string userName);

    /// <summary>
    /// Called when a user starts typing
    /// </summary>
    Task UserTyping(int eventId, int userId, string userName);

    /// <summary>
    /// Called when a user stops typing
    /// </summary>
    Task UserStoppedTyping(int eventId, int userId);
}
