using Shared.Contracts.Messages;

namespace Client.Services;

public interface IChatSignalRService
{
    /// <summary>
    /// Indicates if the SignalR connection is currently active
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Initialize and start the SignalR connection
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stop the SignalR connection
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Join a specific event chat room
    /// </summary>
    Task JoinEventChatAsync(int eventId);

    /// <summary>
    /// Leave a specific event chat room
    /// </summary>
    Task LeaveEventChatAsync(int eventId);

    /// <summary>
    /// Subscribe to new messages for a specific event
    /// Returns IDisposable to unsubscribe
    /// </summary>
    IDisposable OnMessageReceived(int eventId, Func<MessageDto, Task> handler);

    /// <summary>
    /// Subscribe to message deletion events for a specific event
    /// Returns IDisposable to unsubscribe
    /// </summary>
    IDisposable OnMessageDeleted(int eventId, Func<int, string, Task> handler);

    /// <summary>
    /// Subscribe to message flagged events for a specific event
    /// Returns IDisposable to unsubscribe
    /// </summary>
    IDisposable OnMessageFlagged(int eventId, Func<int, int, string, Task> handler);

    /// <summary>
    /// Subscribe to user typing events for a specific event
    /// Returns IDisposable to unsubscribe
    /// </summary>
    IDisposable OnUserTyping(int eventId, Func<int, string, Task> handler);

    /// <summary>
    /// Subscribe to user stopped typing events for a specific event
    /// Returns IDisposable to unsubscribe
    /// </summary>
    IDisposable OnUserStoppedTyping(int eventId, Func<int, Task> handler);

    /// <summary>
    /// Notify other users that current user is typing
    /// </summary>
    Task NotifyTypingAsync(int eventId, string userName);

    /// <summary>
    /// Notify other users that current user stopped typing
    /// </summary>
    Task NotifyStoppedTypingAsync(int eventId);

    /// <summary>
    /// Event fired when connection state changes
    /// </summary>
    event EventHandler<bool>? ConnectionStateChanged;
}
