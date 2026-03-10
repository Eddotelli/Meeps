using System.Security.Claims;
using API.Infrastructure.Data;
using API.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;
using Shared.Enums;

namespace API.Features.Events.JoinEvent;

public class JoinEventHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<JoinEventHandler> _logger;

    public JoinEventHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<JoinEventHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<Result<JoinEventResponse>> HandleAsync(JoinEventRequest request)
    {
        // Get current user ID from claims
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Unauthorized attempt to join event");
            return Result.Failure<JoinEventResponse>(AuthErrors.InvalidCredentials);
        }

        // Get event with participants
        var eventEntity = await _context.Events
            .Include(e => e.EventParticipants)
            .FirstOrDefaultAsync(e => e.Id == request.EventId);

        if (eventEntity == null)
        {
            _logger.LogWarning("User {UserId} attempted to join non-existent event {EventId}", userId, request.EventId);
            return Result.Failure<JoinEventResponse>(EventErrors.NotFound);
        }

        // Check if event is active
        if (eventEntity.Status != EventStatus.Active)
        {
            _logger.LogWarning("User {UserId} attempted to join inactive event {EventId}", userId, request.EventId);
            return Result.Failure<JoinEventResponse>(EventErrors.EventNotActive);
        }

        // Check if event is in the past
        if (eventEntity.DateTime < DateTime.UtcNow)
        {
            _logger.LogWarning("User {UserId} attempted to join past event {EventId}", userId, request.EventId);
            return Result.Failure<JoinEventResponse>(EventErrors.EventHasPassed);
        }

        // Check if user is already a participant
        var existingParticipant = eventEntity.EventParticipants
            .FirstOrDefault(ep => ep.UserId == userId);

        if (existingParticipant != null)
        {
            _logger.LogWarning("User {UserId} is already a participant in event {EventId}", userId, request.EventId);
            return Result.Failure<JoinEventResponse>(EventErrors.AlreadyParticipant);
        }

        // Check if event is full
        var currentAttendance = eventEntity.EventParticipants
            .Count(ep => ep.Status == ParticipantStatus.Accepted);

        if (currentAttendance >= eventEntity.MaxAttendance)
        {
            _logger.LogWarning("User {UserId} attempted to join full event {EventId}", userId, request.EventId);
            return Result.Failure<JoinEventResponse>(EventErrors.EventFull);
        }

        // Create participant entry
        var participant = new EventParticipant
        {
            EventId = request.EventId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow,
            Status = ParticipantStatus.Accepted,
            Role = EventRole.Participant
        };

        _context.EventParticipants.Add(participant);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} joined event {EventId}", userId, request.EventId);

        return Result<JoinEventResponse>.Success(new JoinEventResponse
        {
            EventId = eventEntity.Id,
            EventTitle = eventEntity.Title,
            JoinedAt = participant.JoinedAt,
            Message = "Successfully joined event"
        });
    }
}
