using System.Security.Claims;
using API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;

namespace API.Features.Events.LeaveEvent;

public class LeaveEventHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<LeaveEventHandler> _logger;

    public LeaveEventHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<LeaveEventHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<Result<LeaveEventResponse>> HandleAsync(LeaveEventRequest request)
    {
        // Get current user ID from claims
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Unauthorized attempt to leave event");
            return Result.Failure<LeaveEventResponse>(AuthErrors.InvalidCredentials);
        }

        // Get event with participants
        var eventEntity = await _context.Events
            .Include(e => e.EventParticipants)
            .FirstOrDefaultAsync(e => e.Id == request.EventId);

        if (eventEntity == null)
        {
            _logger.LogWarning("User {UserId} attempted to leave non-existent event {EventId}", userId, request.EventId);
            return Result.Failure<LeaveEventResponse>(EventErrors.NotFound);
        }

        // Check if user is the creator - creators cannot leave their own event
        if (eventEntity.CreatedByUserId == userId)
        {
            _logger.LogWarning("Event creator {UserId} attempted to leave their own event {EventId}", userId, request.EventId);
            return Result.Failure<LeaveEventResponse>(EventErrors.CannotLeaveOwnEvent);
        }

        // Check if user is a participant
        var participant = eventEntity.EventParticipants
            .FirstOrDefault(ep => ep.UserId == userId);

        if (participant == null)
        {
            _logger.LogWarning("User {UserId} attempted to leave event {EventId} but is not a participant", userId, request.EventId);
            return Result.Failure<LeaveEventResponse>(EventErrors.NotParticipant);
        }

        // Remove participant entry
        _context.EventParticipants.Remove(participant);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} left event {EventId}", userId, request.EventId);

        return Result<LeaveEventResponse>.Success(new LeaveEventResponse
        {
            EventId = eventEntity.Id,
            EventTitle = eventEntity.Title,
            LeftAt = DateTime.UtcNow,
            Message = "Successfully left event"
        });
    }
}
