using API.Infrastructure.Data;
using API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;
using Shared.Enums;
using System.Security.Claims;

namespace API.Features.Events.GetMyArchivedEvents;

public class GetMyArchivedEventsHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHashIdService _hashIdService;
    private readonly ILogger<GetMyArchivedEventsHandler> _logger;

    public GetMyArchivedEventsHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IHashIdService hashIdService,
        ILogger<GetMyArchivedEventsHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _hashIdService = hashIdService;
        _logger = logger;
    }

    public async Task<Result<List<GetEventDetailsResponse>>> Handle()
    {
        var userId = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Unauthorized attempt to get archived events");
            return Result.Failure<List<GetEventDetailsResponse>>(AuthErrors.InvalidCredentials);
        }

        var userIdInt = int.Parse(userId);
        _logger.LogInformation("Getting archived events for user {UserId}", userIdInt);

        // Get past events where user is creator OR participant
        var events = await _context.Events
            .Include(e => e.CreatedByUser)
            .Include(e => e.Category)
            .Include(e => e.EventParticipants)
            .Where(e => e.DateTime < DateTime.UtcNow // Past events
                && !e.IsDeleted
                && (e.CreatedByUserId == userIdInt // User is creator
                    || e.EventParticipants.Any(ep => ep.UserId == userIdInt
                        && ep.Status == ParticipantStatus.Accepted))) // Or participant
            .OrderByDescending(e => e.DateTime) // Sort by date (newest first)
            .ToListAsync();

        var response = events.Select(eventEntity => new GetEventDetailsResponse
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
            CurrentAttendance = eventEntity.EventParticipants.Count(p => p.Status == ParticipantStatus.Accepted),
            MinAge = eventEntity.MinAge,
            MaxAge = eventEntity.MaxAge,
            GenderRestriction = eventEntity.GenderRestriction,
            Status = eventEntity.Status,
            ImageUrl = eventEntity.ImageUrl,
            IsPublic = eventEntity.IsPublic,
            IsUserParticipant = eventEntity.EventParticipants.Any(p => p.UserId == userIdInt),
            IsUserCreator = eventEntity.CreatedByUserId == userIdInt
        }).ToList();

        _logger.LogInformation("Found {Count} archived events for user {UserId}", response.Count, userIdInt);
        return Result.Success(response);
    }
}
