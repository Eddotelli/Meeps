using API.Infrastructure.Data;
using API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;
using Shared.Enums;
using System.Security.Claims;

namespace API.Features.Events.GetEventDetails;

public class GetEventDetailsHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHashIdService _hashIdService;
    private readonly ILogger<GetEventDetailsHandler> _logger;

    public GetEventDetailsHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IHashIdService hashIdService,
        ILogger<GetEventDetailsHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _hashIdService = hashIdService;
        _logger = logger;
    }

    public async Task<Result<GetEventDetailsResponse>> Handle(int eventId)
    {
        var eventEntity = await _context.Events
            .Include(e => e.CreatedByUser)
            .Include(e => e.Category)
            .Include(e => e.EventParticipants)
                .ThenInclude(ep => ep.User)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (eventEntity == null)
        {
            return Result.Failure<GetEventDetailsResponse>(EventErrors.NotFound);
        }

        var userId = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        int? userIdInt = null;
        if (!string.IsNullOrEmpty(userId))
        {
            userIdInt = int.Parse(userId);
        }

        // Check if user is blocked from this event
        if (userIdInt.HasValue)
        {
            var isBlocked = eventEntity.EventParticipants
                .Any(p => p.UserId == userIdInt.Value && p.Status == ParticipantStatus.Blocked);

            if (isBlocked)
            {
                _logger.LogWarning("Blocked user {UserId} attempted to view event {EventId}", userIdInt, eventId);
                return Result.Failure<GetEventDetailsResponse>(EventErrors.NotFound);
            }
        }

        var isUserParticipant = userIdInt.HasValue && eventEntity.EventParticipants.Any(p => p.UserId == userIdInt.Value && p.Status != ParticipantStatus.Blocked);
        var isUserCreator = userIdInt.HasValue && eventEntity.CreatedByUserId == userIdInt.Value;

        // Check if event is only for verified users and user is not verified
        if (eventEntity.OnlyVerifiedUsers && userIdInt.HasValue)
        {
            var user = await _context.Users.FindAsync(userIdInt.Value);
            if (user != null && !user.IsVerified && !isUserCreator && !isUserParticipant)
            {
                _logger.LogWarning("User {UserId} attempted to view verified-only event {EventId} but is not verified", userIdInt, eventId);
                return Result.Failure<GetEventDetailsResponse>(EventErrors.NotFound);
            }
        }

        _logger.LogInformation("GetEventDetails - EventId: {EventId}, UserId: {UserId}, IsUserParticipant: {IsParticipant}, IsUserCreator: {IsCreator}",
            eventEntity.Id, userIdInt, isUserParticipant, isUserCreator);
        _logger.LogInformation("GetEventDetails - EventParticipants count: {Count}", eventEntity.EventParticipants.Count);

        foreach (var participant in eventEntity.EventParticipants)
        {
            _logger.LogInformation("  - Participant UserId: {UserId}, Role: {Role}, Status: {Status}",
                participant.UserId, participant.Role, participant.Status);
        }

        var response = new GetEventDetailsResponse
        {
            Id = eventEntity.Id,
            EventHash = _hashIdService.Encode(eventEntity.Id),
            Title = eventEntity.Title,
            Description = eventEntity.Description,
            Location = eventEntity.Location,
            Latitude = eventEntity.Latitude,
            Longitude = eventEntity.Longitude,
            DateTime = eventEntity.DateTime,
            CreatedAt = eventEntity.CreatedAt,
            CreatedByUserId = eventEntity.CreatedByUserId,
            CreatedByUserName = eventEntity.CreatedByUser?.UserName ?? "Unknown",
            CategoryId = eventEntity.CategoryId,
            Category = eventEntity.Category?.Type ?? CategoryType.Other,
            MinAttendance = eventEntity.MinAttendance,
            MaxAttendance = eventEntity.MaxAttendance,
            CurrentAttendance = eventEntity.EventParticipants.Count(p => p.Status == Shared.Enums.ParticipantStatus.Accepted),
            MinAge = eventEntity.MinAge,
            MaxAge = eventEntity.MaxAge,
            GenderRestriction = eventEntity.GenderRestriction,
            Status = eventEntity.Status,
            ImageUrl = eventEntity.ImageUrl,
            IsPublic = eventEntity.IsPublic,
            OnlyVerifiedUsers = eventEntity.OnlyVerifiedUsers,
            IsUserParticipant = isUserParticipant,
            IsUserCreator = isUserCreator,
            Participants = eventEntity.EventParticipants
                .Where(p => p.Status != ParticipantStatus.Blocked)
                .Select(p => new EventParticipantDto
                {
                    UserId = p.UserId,
                    Username = p.User?.UserName ?? "Unknown",
                    DisplayName = p.User?.DisplayName ?? p.User?.UserName ?? "Unknown",
                    ProfileImageUrl = p.User?.ProfileImageUrl,
                    Age = p.User?.BirthDate != null ?
                        DateTime.UtcNow.Year - p.User.BirthDate.Value.Year -
                        (DateTime.UtcNow.DayOfYear < p.User.BirthDate.Value.DayOfYear ? 1 : 0) : null,
                    Gender = p.User?.Gender,
                    IsVerified = p.User?.IsVerified ?? false,
                    Status = p.Status,
                    Role = p.Role,
                    JoinedAt = p.JoinedAt,
                    BlockedAt = p.BlockedAt,
                    BlockedReason = p.BlockedReason
                })
                .OrderByDescending(p => p.Role) // Organizer first
                .ThenBy(p => p.JoinedAt) // Then by join date
                .ToList(),
            BlockedParticipants = eventEntity.EventParticipants
                .Where(p => p.Status == ParticipantStatus.Blocked)
                .Select(p => new EventParticipantDto
                {
                    UserId = p.UserId,
                    Username = p.User?.UserName ?? "Unknown",
                    DisplayName = p.User?.DisplayName ?? p.User?.UserName ?? "Unknown",
                    ProfileImageUrl = p.User?.ProfileImageUrl,
                    Age = p.User?.BirthDate != null ?
                        DateTime.UtcNow.Year - p.User.BirthDate.Value.Year -
                        (DateTime.UtcNow.DayOfYear < p.User.BirthDate.Value.DayOfYear ? 1 : 0) : null,
                    Gender = p.User?.Gender,
                    IsVerified = p.User?.IsVerified ?? false,
                    Status = p.Status,
                    Role = p.Role,
                    JoinedAt = p.JoinedAt,
                    BlockedAt = p.BlockedAt,
                    BlockedReason = p.BlockedReason
                })
                .OrderBy(p => p.BlockedAt)
                .ToList()
        };

        return Result.Success(response);
    }
}
