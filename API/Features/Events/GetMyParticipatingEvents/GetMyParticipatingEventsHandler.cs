using API.Infrastructure.Data;
using API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;
using Shared.Enums;
using System.Security.Claims;

namespace API.Features.Events.GetMyParticipatingEvents;

public class GetMyParticipatingEventsHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHashIdService _hashIdService;
    private readonly ILogger<GetMyParticipatingEventsHandler> _logger;

    public GetMyParticipatingEventsHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IHashIdService hashIdService,
        ILogger<GetMyParticipatingEventsHandler> logger)
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
            _logger.LogWarning("Unauthorized attempt to get participating events");
            return Result.Failure<List<GetEventDetailsResponse>>(AuthErrors.InvalidCredentials);
        }

        var userIdInt = int.Parse(userId);
        _logger.LogInformation("Getting participating events for user {UserId}", userIdInt);

        // Get events where user is a participant and event is active and upcoming
        var events = await _context.EventParticipants
            .Include(ep => ep.Event)
                .ThenInclude(e => e.CreatedByUser)
            .Include(ep => ep.Event)
                .ThenInclude(e => e.Category)
            .Include(ep => ep.Event)
                .ThenInclude(e => e.EventParticipants)
            .Where(ep => ep.UserId == userIdInt
                && ep.Status == ParticipantStatus.Accepted
                && !ep.Event.IsDeleted
                && ep.Event.DateTime >= DateTime.UtcNow) // Only upcoming events
            .Select(ep => ep.Event)
            .OrderBy(e => e.DateTime) // Sort by date (nearest first)
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
            IsUserParticipant = true, // Always true since we're querying participant events
            IsUserCreator = eventEntity.CreatedByUserId == userIdInt
        }).ToList();

        _logger.LogInformation("Found {Count} participating events for user {UserId}", response.Count, userIdInt);
        return Result.Success(response);
    }
}
