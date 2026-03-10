using System.Security.Claims;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Messages;
using Shared.Enums;

namespace API.Features.Messages.GetEventMessages;

public class GetEventMessagesHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHashIdService _hashIdService;
    private readonly ILogger<GetEventMessagesHandler> _logger;
    private readonly IEventPermissionsService _permissionsService;

    public GetEventMessagesHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IHashIdService hashIdService,
        ILogger<GetEventMessagesHandler> logger,
        IEventPermissionsService permissionsService)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _hashIdService = hashIdService;
        _logger = logger;
        _permissionsService = permissionsService;
    }

    public async Task<Result<GetEventMessagesResponse>> HandleAsync(GetEventMessagesRequest request)
    {
        // 1. Get current user ID from JWT claims
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Unauthorized attempt to get messages");
            return Result.Failure<GetEventMessagesResponse>(CommonErrors.Unauthorized);
        }

        // 2. Decode event hash to get event ID
        var eventId = _hashIdService.Decode(request.EventHash);
        if (eventId == null)
        {
            _logger.LogWarning("Invalid event hash provided: {EventHash}", request.EventHash);
            return Result.Failure<GetEventMessagesResponse>(MessageErrors.EventNotFound);
        }

        // 3. Apply default values for pagination
        var pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 50;

        // 4. Verify event exists
        var eventExists = await _context.Events
            .AnyAsync(e => e.Id == eventId.Value);

        if (!eventExists)
        {
            _logger.LogWarning("User {UserId} attempted to get messages from non-existent event {EventId}",
                userId, eventId.Value);
            return Result.Failure<GetEventMessagesResponse>(MessageErrors.EventNotFound);
        }

        // 5. Check if user has permission to access chat (must be accepted participant with active status)
        if (!await _permissionsService.CanAccessChat(userId, eventId.Value))
        {
            _logger.LogWarning("User {UserId} does not have permission to view messages in event {EventId}",
                userId, eventId.Value);
            return Result.Failure<GetEventMessagesResponse>(MessageErrors.UnauthorizedAccess);
        }

        // 6. Get total count of messages (exclude deleted and flagged)
        var totalCount = await _context.Messages
            .Where(m => m.EventId == eventId.Value
                && !m.IsDeletedByModeration
                && !m.IsFlagged)
            .CountAsync();

        // 7. Get paginated messages with user info (exclude deleted and flagged)
        // Fetch newest messages first, then reverse for chronological display
        var messages = await _context.Messages
            .Where(m => m.EventId == eventId.Value
                && !m.IsDeletedByModeration
                && !m.IsFlagged)
            .OrderByDescending(m => m.SentAt)  // Newest first for pagination
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MessageDto
            {
                MessageId = m.Id,
                EventId = m.EventId,
                EventHash = _hashIdService.Encode(m.EventId),
                UserId = m.UserId,
                UserName = m.User.IsDeleted ? "Deleted User" : (m.User.DisplayName ?? string.Empty),
                ProfileImageUrl = m.User.IsDeleted ? null : m.User.ProfileImageUrl,
                Text = m.Text,
                SentAt = m.SentAt,
                IsAuthorStillParticipant = _context.EventParticipants
                    .Any(ep => ep.EventId == m.EventId && ep.UserId == m.UserId),
                IsUserBlocked = _context.EventParticipants
                    .Where(ep => ep.EventId == m.EventId && ep.UserId == m.UserId)
                    .Select(ep => ep.BlockedAt != null)
                    .FirstOrDefault()
            })
            .ToListAsync();

        // Reverse to get chronological order (oldest first) for display
        messages.Reverse();

        _logger.LogInformation(
            "User {UserId} retrieved {Count} messages from event {EventId} (Page {Page})",
            userId, messages.Count, eventId.Value, pageNumber);

        // 8. Return success response
        return Result<GetEventMessagesResponse>.Success(new GetEventMessagesResponse
        {
            Messages = messages,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        });
    }
}
