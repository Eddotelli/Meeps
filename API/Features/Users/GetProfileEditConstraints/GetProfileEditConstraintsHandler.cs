using System.Security.Claims;
using API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Users;
using Shared.Enums;
using Shared.Extensions;

namespace API.Features.Users.GetProfileEditConstraints;

public class GetProfileEditConstraintsHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GetProfileEditConstraintsHandler> _logger;

    public GetProfileEditConstraintsHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GetProfileEditConstraintsHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<Result<GetProfileEditConstraintsResponse>> Handle()
    {
        _logger.LogInformation("Getting profile edit constraints");

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            _logger.LogWarning("Unauthorized attempt to get profile edit constraints");
            return Result.Failure<GetProfileEditConstraintsResponse>(UserErrors.Unauthorized);
        }

        var user = await _context.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.Gender, u.BirthDate })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId.Value);
            return Result.Failure<GetProfileEditConstraintsResponse>(UserErrors.NotFound);
        }

        // Get all future events created by this user
        var futureEvents = await _context.Events
            .Where(e => e.CreatedByUserId == userId.Value)
            .Where(e => e.DateTime > DateTime.UtcNow)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.DateTime,
                e.MinAge,
                e.MaxAge,
                e.GenderRestriction
            })
            .ToListAsync();

        _logger.LogInformation("User {UserId} has {Count} future events", userId.Value, futureEvents.Count);

        // Check gender conflicts
        var genderConflictingEvents = new List<ConflictingEvent>();
        bool canChangeGender = true;

        // Check if user has any events with gender-specific restrictions
        // If they do, they cannot change their gender (regardless of what they currently have or want to change to)
        var eventsWithSpecificGenderRestrictions = futureEvents
            .Where(e => e.GenderRestriction == GenderRestriction.MaleOnly ||
                       e.GenderRestriction == GenderRestriction.FemaleOnly ||
                       e.GenderRestriction == GenderRestriction.NonBinaryOnly)
            .ToList();

        if (eventsWithSpecificGenderRestrictions.Any())
        {
            // User has events with gender restrictions - they cannot change their gender
            foreach (var evt in eventsWithSpecificGenderRestrictions)
            {
                genderConflictingEvents.Add(new ConflictingEvent
                {
                    EventId = evt.Id,
                    Title = evt.Title,
                    DateTime = evt.DateTime,
                    GenderRestriction = evt.GenderRestriction
                });
            }
            canChangeGender = false;
        }

        // Check age conflicts
        var ageConflictingEvents = new List<ConflictingEvent>();
        bool canChangeBirthDate = true;

        if (user.BirthDate.HasValue)
        {
            var eventsWithAgeRestrictions = futureEvents
                .Where(e => e.MinAge.HasValue || e.MaxAge.HasValue)
                .Where(e => !(e.MinAge == 18 && e.MaxAge == 99)) // Exclude "all ages" restrictions
                .ToList();

            if (eventsWithAgeRestrictions.Any())
            {
                // User has events with age restrictions set
                // Changing birth date could violate these restrictions
                foreach (var evt in eventsWithAgeRestrictions)
                {
                    ageConflictingEvents.Add(new ConflictingEvent
                    {
                        EventId = evt.Id,
                        Title = evt.Title,
                        DateTime = evt.DateTime,
                        MinAge = evt.MinAge,
                        MaxAge = evt.MaxAge
                    });
                    canChangeBirthDate = false;
                }
            }
        }

        // Build warnings
        var warnings = new List<string>();
        if (!canChangeGender)
        {
            warnings.Add($"Cannot change gender - {genderConflictingEvents.Count} event(s) with gender restrictions");
        }
        if (!canChangeBirthDate)
        {
            warnings.Add($"Cannot change birth date - {ageConflictingEvents.Count} event(s) with age restrictions");
        }

        var response = new GetProfileEditConstraintsResponse
        {
            CanChangeGender = canChangeGender,
            CanChangeBirthDate = canChangeBirthDate,
            GenderConflictingEvents = genderConflictingEvents,
            AgeConflictingEvents = ageConflictingEvents,
            Warnings = warnings
        };

        _logger.LogInformation("Profile edit constraints calculated for user {UserId}: CanChangeGender={CanChangeGender}, CanChangeBirthDate={CanChangeBirthDate}",
            userId.Value, canChangeGender, canChangeBirthDate);

        return Result.Success(response);
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return null;

        return userId;
    }
}
