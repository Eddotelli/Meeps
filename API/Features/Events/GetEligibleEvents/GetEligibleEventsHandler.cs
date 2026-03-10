using API.Infrastructure.Data;
using API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;
using Shared.Enums;
using Shared.Extensions;
using System.Security.Claims;

namespace API.Features.Events.GetEligibleEvents;

public class GetEligibleEventsHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHashIdService _hashIdService;
    private readonly ILogger<GetEligibleEventsHandler> _logger;

    public GetEligibleEventsHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IHashIdService hashIdService,
        ILogger<GetEligibleEventsHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _hashIdService = hashIdService;
        _logger = logger;
    }

    public async Task<Result<GetEligibleEventsResponse>> Handle(GetEligibleEventsRequest request)
    {
        var userId = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("GetEligibleEvents called without authenticated user");
            return Result.Failure<GetEligibleEventsResponse>(CommonErrors.Unauthorized);
        }

        var userIdInt = int.Parse(userId);
        var user = await _context.Users.FindAsync(userIdInt);

        if (user == null)
        {
            _logger.LogError("User not found: {UserId}", userIdInt);
            return Result.Failure<GetEligibleEventsResponse>(UserErrors.NotFound);
        }

        _logger.LogInformation("Getting eligible events for user {UserId}", userIdInt);

        // Determine search location and radius
        var searchLat = request.Latitude ?? user.DefaultCityLatitude;
        var searchLon = request.Longitude ?? user.DefaultCityLongitude;
        var searchRadius = request.RadiusKm ?? user.SearchRadius;

        if (!searchLat.HasValue || !searchLon.HasValue)
        {
            _logger.LogWarning("No location available for user {UserId}", userIdInt);
            return Result.Failure<GetEligibleEventsResponse>(LocationErrors.NotSet);
        }

        // Calculate user age if birth date is set
        int? userAge = null;
        if (user.BirthDate.HasValue)
        {
            var today = DateTime.UtcNow;
            userAge = today.Year - user.BirthDate.Value.Year;
            if (user.BirthDate.Value.Date > today.AddYears(-userAge.Value))
            {
                userAge--;
            }
        }

        // Start building query
        var query = _context.Events
            .Include(e => e.Category)
            .Include(e => e.EventParticipants)
            .Where(e => e.Status == EventStatus.Active)
            .Where(e => e.IsPublic)
            .AsQueryable();

        // Filter by verified users restriction
        if (!user.IsVerified)
        {
            // If user is not verified, exclude events that are only for verified users
            query = query.Where(e => !e.OnlyVerifiedUsers);
        }

        // Filter by gender restriction using type-safe enum matching
        if (user.Gender.HasValue)
        {
            var userGender = user.Gender.Value;
            query = query.Where(e =>
                e.GenderRestriction == GenderRestriction.None ||
                (e.GenderRestriction == GenderRestriction.MaleOnly && userGender == Gender.Male) ||
                (e.GenderRestriction == GenderRestriction.FemaleOnly && userGender == Gender.Female) ||
                (e.GenderRestriction == GenderRestriction.NonBinaryOnly && userGender == Gender.NonBinary)
            );
        }
        else
        {
            // If user hasn't set gender, only show events with no gender restriction
            query = query.Where(e => e.GenderRestriction == GenderRestriction.None);
        }

        // Filter by age restrictions
        if (userAge.HasValue)
        {
            query = query.Where(e =>
                (e.MinAge == null || userAge >= e.MinAge) &&
                (e.MaxAge == null || userAge <= e.MaxAge)
            );
        }
        else
        {
            // If user hasn't set birth date, show events with no age restrictions (null)
            // This allows new users to see events that are open to everyone
            query = query.Where(e => e.MinAge == null && e.MaxAge == null);
        }

        // Filter by category if specified
        if (request.CategoryId.HasValue)
        {
            query = query.Where(e => e.CategoryId == request.CategoryId.Value);
        }

        // Get all events that match criteria (before location filtering)
        var events = await query.ToListAsync();

        _logger.LogInformation("Found {Count} events before location filtering", events.Count);

        // Filter by location and calculate distances
        var eventsWithDistance = events
            .Where(e => e.Latitude.HasValue && e.Longitude.HasValue)
            .Select(e => new
            {
                Event = e,
                Distance = CalculateDistance(
                    searchLat.Value,
                    searchLon.Value,
                    e.Latitude!.Value,
                    e.Longitude!.Value
                ),
                IsUserParticipant = e.EventParticipants.Any(p => p.UserId == userIdInt),
                IsUserCreator = e.CreatedByUserId == userIdInt,
                IsUserBlocked = e.EventParticipants.Any(p => p.UserId == userIdInt && p.Status == ParticipantStatus.Blocked)
            })
            .Where(x => x.Distance <= searchRadius)
            .Where(x => !x.IsUserBlocked) // Exclude events where user is blocked
            .Where(x =>
            {
                // Apply date filtering based on request.StartDate or current time
                var cutoffDate = request.StartDate ?? DateTime.UtcNow;
                var isPassed = x.Event.DateTime <= cutoffDate;

                // Filter out passed events (unless user is participant or creator)
                if (isPassed && !x.IsUserParticipant && !x.IsUserCreator)
                {
                    return false;
                }

                // Filter out full events (unless user is participant or creator)
                var currentAttendance = x.Event.EventParticipants.Count;
                var isFull = x.Event.MaxAttendance > 0 && currentAttendance >= x.Event.MaxAttendance;
                if (isFull && !x.IsUserParticipant && !x.IsUserCreator)
                {
                    return false;
                }

                return true;
            })
            .ToList();

        _logger.LogInformation("Found {Count} events within {Radius}km", eventsWithDistance.Count, searchRadius);

        // Apply sorting
        eventsWithDistance = request.SortBy switch
        {
            "distance" => eventsWithDistance.OrderBy(x => x.Distance).ToList(),
            "date" => eventsWithDistance.OrderBy(x => x.Event.DateTime).ToList(),
            "name" => eventsWithDistance.OrderBy(x => x.Event.Title).ToList(),
            "attendees" => eventsWithDistance.OrderByDescending(x => x.Event.EventParticipants.Count).ToList(),
            "spotsLeft" => eventsWithDistance.OrderBy(x => x.Event.MaxAttendance - x.Event.EventParticipants.Count).ToList(),
            _ => eventsWithDistance.OrderBy(x => x.Distance).ToList()
        };

        // Get total count before pagination
        var totalCount = eventsWithDistance.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        // Apply pagination
        var paginatedEvents = eventsWithDistance
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // Map to response DTOs
        var eventDtos = paginatedEvents.Select(x => new EligibleEventDto
        {
            Id = x.Event.Id,
            EventHash = _hashIdService.Encode(x.Event.Id),
            Title = x.Event.Title,
            Description = x.Event.Description,
            Location = x.Event.Location,
            Latitude = x.Event.Latitude,
            Longitude = x.Event.Longitude,
            DateTime = x.Event.DateTime,
            CreatedAt = x.Event.CreatedAt,
            CategoryId = x.Event.CategoryId,
            Category = x.Event.Category.Type,
            MinAttendance = x.Event.MinAttendance,
            MaxAttendance = x.Event.MaxAttendance,
            CurrentAttendance = x.Event.EventParticipants.Count,
            MinAge = x.Event.MinAge,
            MaxAge = x.Event.MaxAge,
            GenderRestriction = x.Event.GenderRestriction,
            ImageUrl = x.Event.ImageUrl,
            OnlyVerifiedUsers = x.Event.OnlyVerifiedUsers,
            DistanceKm = Math.Round(x.Distance, 2),
            IsUserParticipant = x.IsUserParticipant,
            IsUserCreator = x.IsUserCreator
        }).ToList();

        var response = new GetEligibleEventsResponse
        {
            Events = eventDtos,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalPages = totalPages,
            SearchLatitude = searchLat.Value,
            SearchLongitude = searchLon.Value,
            SearchRadiusKm = searchRadius
        };

        _logger.LogInformation("Returning {Count} eligible events for user {UserId}", eventDtos.Count, userIdInt);

        return Result<GetEligibleEventsResponse>.Success(response);
    }

    /// <summary>
    /// Calculates distance between two coordinates using Haversine formula.
    /// </summary>
    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth's radius in kilometers

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var distance = R * c;

        return distance;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
