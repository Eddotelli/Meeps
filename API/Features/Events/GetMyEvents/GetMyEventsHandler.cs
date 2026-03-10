using API.Infrastructure.Data;
using API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;
using Shared.Enums;
using System.Security.Claims;

namespace API.Features.Events.GetMyEvents;

public class GetMyEventsHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHashIdService _hashIdService;
    private readonly ILogger<GetMyEventsHandler> _logger;

    public GetMyEventsHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IHashIdService hashIdService,
        ILogger<GetMyEventsHandler> logger)
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
            _logger.LogWarning("Unauthorized attempt to get user events");
            return Result.Failure<List<GetEventDetailsResponse>>(AuthErrors.InvalidCredentials);
        }

        var userIdInt = int.Parse(userId);
        _logger.LogInformation("Getting events for user {UserId}", userIdInt);

        var events = await _context.Events
            .Include(e => e.CreatedByUser)
            .Include(e => e.Category)
            .Include(e => e.EventParticipants)
            .Where(e => e.CreatedByUserId == userIdInt)
            .OrderByDescending(e => e.CreatedAt)
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
            CurrentAttendance = eventEntity.EventParticipants.Count(p => p.Status == Shared.Enums.ParticipantStatus.Accepted),
            MinAge = eventEntity.MinAge,
            MaxAge = eventEntity.MaxAge,
            GenderRestriction = eventEntity.GenderRestriction,
            Status = eventEntity.Status,
            ImageUrl = eventEntity.ImageUrl,
            IsPublic = eventEntity.IsPublic,
            IsUserParticipant = eventEntity.EventParticipants.Any(p => p.UserId == userIdInt),
            IsUserCreator = true // Always true since we're querying created events
        }).ToList();

        _logger.LogInformation("Found {Count} events for user {UserId}", response.Count, userIdInt);

        return Result.Success(response);
    }
}
