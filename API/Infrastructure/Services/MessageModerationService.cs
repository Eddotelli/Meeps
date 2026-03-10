using API.Common.BackgroundTasks;
using API.Infrastructure.Data;
using API.Infrastructure.Hubs;
using API.Infrastructure.Services;
using API.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace API.Infrastructure.Services;

public class MessageModerationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<MessageModerationService> _logger;

    public MessageModerationService(
        IServiceProvider serviceProvider,
        IBackgroundTaskQueue taskQueue,
        ILogger<MessageModerationService> logger)
    {
        _serviceProvider = serviceProvider;
        _taskQueue = taskQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🤖 Message Moderation Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messageId = await _taskQueue.DequeueAsync(stoppingToken);
                
                _logger.LogInformation("⚙️ Processing moderation for message {MessageId}", messageId);

                // Process in background without blocking
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ModerateMessageAsync(messageId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error moderating message {MessageId}", messageId);
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in message moderation service");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("🛑 Message Moderation Background Service stopped");
    }

    private async Task ModerateMessageAsync(int messageId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var moderationService = scope.ServiceProvider.GetRequiredService<IGeminiModerationService>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ChatHub, IChatClient>>();

        // 1. Fetch message from database
        var message = await context.Messages
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
        {
            _logger.LogWarning("Message {MessageId} not found for moderation", messageId);
            return;
        }

        _logger.LogInformation("🤖 Starting Gemini moderation for message {MessageId}", messageId);

        // 2. Call Gemini for moderation
        var moderationResult = await moderationService.ModerateMessageAsync(message.Text.Trim());

        if (moderationResult.IsFailure)
        {
            _logger.LogWarning(
                "Moderation service failed for message {MessageId}: {Error}",
                messageId, moderationResult.Error?.Message);
            return;
        }

        var moderation = moderationResult.Value;

        // 3. Update message with moderation results
        message.IsFlagged = moderation.IsInappropriate && moderation.Severity >= 4;
        message.ModerationSeverity = moderation.Severity;
        message.ModerationCategory = moderation.Category;

        _logger.LogInformation(
            "Moderation complete for message {MessageId} - Severity: {Severity}, Category: {Category}",
            messageId, moderation.Severity, moderation.Category);

        // 4. Handle based on severity
        if (moderation.Severity >= 7)
        {
            // BLOCK: Delete message
            _logger.LogWarning(
                "🛑 DELETING MESSAGE {MessageId} - Severity: {Severity}/10, Category: {Category}, Reason: {Reason}",
                messageId, moderation.Severity, moderation.Category, moderation.Reason);

            message.IsDeletedByModeration = true;
            message.DeletedByModerationAt = DateTime.UtcNow;

            await context.SaveChangesAsync();

            // Notify all clients in the event group
            var groupName = $"event_{message.EventId}";
            await hubContext.Clients.Group(groupName)
                .MessageDeleted(message.EventId, messageId, moderation.Reason ?? "Inappropriate content");

            _logger.LogInformation(
                "📡 Broadcasted deletion for message {MessageId} to group '{GroupName}'",
                messageId, groupName);
        }
        else if (moderation.Severity >= 5)
        {
            // WARN: Send warning notification
            _logger.LogInformation(
                "⚠️ FLAGGING MESSAGE {MessageId} WITH WARNING - Severity: {Severity}/10, Category: {Category}",
                messageId, moderation.Severity, moderation.Category);

            await context.SaveChangesAsync();

            // Notify the specific user who sent the message
            var groupName = $"event_{message.EventId}";
            await hubContext.Clients.Group(groupName)
                .MessageFlagged(message.EventId, messageId, message.UserId, moderation.Reason ?? "Please be respectful");

            _logger.LogInformation(
                "📡 Sent warning for message {MessageId} to user {UserId}",
                messageId, message.UserId);
        }
        else if (moderation.Severity >= 4)
        {
            // FLAG SILENTLY
            _logger.LogInformation(
                "🔍 FLAGGED MESSAGE {MessageId} SILENTLY - Severity: {Severity}/10",
                messageId, moderation.Severity);

            await context.SaveChangesAsync();
        }
        else
        {
            // OK - Just update severity info
            await context.SaveChangesAsync();
            
            _logger.LogInformation(
                "✅ Message {MessageId} is acceptable - Severity: {Severity}/10",
                messageId, moderation.Severity);
        }
    }
}
