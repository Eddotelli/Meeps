using System.Security.Claims;
using API.Common.BackgroundTasks;
using API.Infrastructure.Data;
using API.Infrastructure.Hubs;
using API.Infrastructure.Services;
using API.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Messages;
using Shared.Enums;

namespace API.Features.Messages.SendMessage;

public class SendMessageHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SendMessageHandler> _logger;
    private readonly IEventPermissionsService _permissionsService;
    private readonly IHubContext<ChatHub, IChatClient> _hubContext;
    private readonly IBackgroundTaskQueue _taskQueue;

    public SendMessageHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SendMessageHandler> logger,
        IEventPermissionsService permissionsService,
        IHubContext<ChatHub, IChatClient> hubContext,
        IBackgroundTaskQueue taskQueue)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _permissionsService = permissionsService;
        _hubContext = hubContext;
        _taskQueue = taskQueue;
    }

    public async Task<Result<SendMessageResponse>> HandleAsync(SendMessageRequest request)
    {
        _logger.LogInformation(
            "📨 Incoming message request - Event: {EventId}, Message length: {Length} chars",
            request.EventId, request.Text?.Length ?? 0);

        // 1. Get current user ID from JWT claims
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("🚫 Unauthorized message attempt - No valid user ID in JWT claims");
            return Result.Failure<SendMessageResponse>(CommonErrors.Unauthorized);
        }

        _logger.LogInformation("👤 User {UserId} sending message to event {EventId}", userId, request.EventId);

        // 2. Verify event exists
        var eventExists = await _context.Events
            .AnyAsync(e => e.Id == request.EventId);

        if (!eventExists)
        {
            _logger.LogWarning("User {UserId} attempted to send message to non-existent event {EventId}",
                userId, request.EventId);
            return Result.Failure<SendMessageResponse>(MessageErrors.EventNotFound);
        }

        // 3. Check if user has permission to access chat (must be accepted participant with active status)
        if (!await _permissionsService.CanAccessChat(userId, request.EventId))
        {
            _logger.LogWarning("User {UserId} does not have permission to send messages in event {EventId}",
                userId, request.EventId);
            return Result.Failure<SendMessageResponse>(MessageErrors.UnauthorizedAccess);
        }

        // 4. Create and save message IMMEDIATELY (moderation happens in background)
        var message = new Message
        {
            EventId = request.EventId,
            UserId = userId,
            Text = request.Text!.Trim(), // Validator ensures Text is not null
            SentAt = DateTime.UtcNow
            // Moderation fields will be updated by background service
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "⚡ Message saved instantly - User: {UserId}, MessageId: {MessageId}, Event: {EventId}",
            userId, message.Id, request.EventId);

        // 5. Queue message for background moderation
        _taskQueue.QueueMessageModeration(message.Id);

        _logger.LogInformation(
            "📤 Queued message {MessageId} for background moderation",
            message.Id);

        // 6. Get user info for response
        var user = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName, u.ProfileImageUrl, u.IsDeleted })
            .FirstOrDefaultAsync();

        // 7. Broadcast message to all connected clients in this event's group IMMEDIATELY
        var messageDto = new MessageDto
        {
            MessageId = message.Id,
            EventId = message.EventId,
            UserId = message.UserId,
            UserName = user?.IsDeleted == true ? "Deleted User" : (user?.DisplayName ?? "Unknown"),
            ProfileImageUrl = user?.IsDeleted == true ? null : user?.ProfileImageUrl,
            Text = message.Text,
            SentAt = message.SentAt,
            IsAuthorStillParticipant = true  // Author is still participant since they just sent the message
        };

        try
        {
            var groupName = $"event_{request.EventId}";
            await _hubContext.Clients
                .Group(groupName)
                .ReceiveMessage(request.EventId, messageDto);

            _logger.LogInformation(
                "📡 Broadcasted message {MessageId} to SignalR group '{GroupName}'",
                message.Id, groupName);
        }
        catch (Exception ex)
        {
            // Don't fail the request if SignalR broadcast fails
            // The message is already saved in the database
            _logger.LogError(
                ex,
                "Failed to broadcast message {MessageId} via SignalR",
                message.Id);
        }

        // 8. Return success response (moderation happens in background)
        return Result<SendMessageResponse>.Success(new SendMessageResponse
        {
            MessageId = message.Id,
            EventId = message.EventId,
            UserId = message.UserId,
            UserName = user?.IsDeleted == true ? "Deleted User" : (user?.DisplayName ?? "Unknown"),
            ProfileImageUrl = user?.IsDeleted == true ? null : user?.ProfileImageUrl,
            Text = message.Text,
            SentAt = message.SentAt,
            IsAuthorStillParticipant = true
            // Moderation fields not included - user gets instant response
        });
    }
}
