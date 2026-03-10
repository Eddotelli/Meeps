using System.Security.Claims;
using API.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Shared.Contracts.Messages;

namespace API.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub for real-time event chat functionality
/// Handles connection management and message broadcasting
/// </summary>
[Authorize] // Require JWT authentication
public class ChatHub : Hub<IChatClient>
{
    private readonly IEventPermissionsService _permissionsService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IEventPermissionsService permissionsService,
        ILogger<ChatHub> logger)
    {
        _permissionsService = permissionsService;
        _logger = logger;
    }

    /// <summary>
    /// Called when a client wants to join an event chat room
    /// Only participants can join
    /// </summary>
    public async Task JoinEventChat(int eventId)
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            _logger.LogWarning("Unauthorized user attempted to join event chat {EventId}", eventId);
            throw new HubException("Unauthorized");
        }

        // Verify user has permission to access chat
        if (!await _permissionsService.CanAccessChat(userId.Value, eventId))
        {
            _logger.LogWarning(
                "User {UserId} attempted to join event chat {EventId} without permission",
                userId, eventId);
            throw new HubException("You do not have permission to access this chat");
        }

        // Add user to SignalR group for this event
        var groupName = GetEventGroupName(eventId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "User {UserId} joined event chat {EventId} (Connection: {ConnectionId})",
            userId, eventId, Context.ConnectionId);
    }

    /// <summary>
    /// Called when a client wants to leave an event chat room
    /// </summary>
    public async Task LeaveEventChat(int eventId)
    {
        var userId = GetCurrentUserId();
        var groupName = GetEventGroupName(eventId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "User {UserId} left event chat {EventId} (Connection: {ConnectionId})",
            userId, eventId, Context.ConnectionId);
    }

    /// <summary>
    /// Called when a user starts typing in an event chat
    /// </summary>
    public async Task NotifyTyping(int eventId, string userName)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return;

        var groupName = GetEventGroupName(eventId);
        
        // Broadcast to all users in the event except the sender
        await Clients.OthersInGroup(groupName).UserTyping(eventId, userId.Value, userName);

        _logger.LogDebug(
            "User {UserId} is typing in event chat {EventId}",
            userId, eventId);
    }

    /// <summary>
    /// Called when a user stops typing in an event chat
    /// </summary>
    public async Task NotifyStoppedTyping(int eventId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return;

        var groupName = GetEventGroupName(eventId);
        
        // Broadcast to all users in the event except the sender
        await Clients.OthersInGroup(groupName).UserStoppedTyping(eventId, userId.Value);

        _logger.LogDebug(
            "User {UserId} stopped typing in event chat {EventId}",
            userId, eventId);
    }

    /// <summary>
    /// Override connection lifecycle for logging
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation(
            "User {UserId} connected to SignalR (Connection: {ConnectionId})",
            userId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();

        if (exception != null)
        {
            _logger.LogError(
                exception,
                "User {UserId} disconnected from SignalR with error (Connection: {ConnectionId})",
                userId, Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation(
                "User {UserId} disconnected from SignalR (Connection: {ConnectionId})",
                userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Helper method to get current authenticated user ID from JWT claims
    /// </summary>
    private int? GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return userId;
    }

    /// <summary>
    /// Helper method to generate consistent group names for events
    /// </summary>
    private static string GetEventGroupName(int eventId) => $"event_{eventId}";
}
