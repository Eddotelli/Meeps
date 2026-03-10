using API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Events;
using Shared.Enums;
using Shared.Extensions;
using System.Security.Claims;

namespace API.Features.Events.GetEventEditConstraints;

public class GetEventEditConstraintsHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GetEventEditConstraintsHandler> _logger;

    public GetEventEditConstraintsHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GetEventEditConstraintsHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<Result<GetEventEditConstraintsResponse>> Handle(int eventId)
    {
        _logger.LogInformation("Getting edit constraints for event {EventId}", eventId);

        // Get current user
        var userId = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Unauthorized attempt to get edit constraints for event {EventId}", eventId);
            return Result.Failure<GetEventEditConstraintsResponse>(AuthErrors.InvalidCredentials);
        }

        var userIdInt = int.Parse(userId);

        // Get current user's gender for filtering restrictions
        var currentUser = await _context.Users
            .Where(u => u.Id == userIdInt)
            .Select(u => new { u.Gender })
            .FirstOrDefaultAsync();

        if (currentUser == null)
        {
            _logger.LogWarning("User {UserId} not found", userIdInt);
            return Result.Failure<GetEventEditConstraintsResponse>(AuthErrors.InvalidCredentials);
        }

        // Get event with participants
        var eventEntity = await _context.Events
            .Include(e => e.EventParticipants)
                .ThenInclude(ep => ep.User)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (eventEntity == null)
        {
            _logger.LogWarning("Event {EventId} not found", eventId);
            return Result.Failure<GetEventEditConstraintsResponse>(EventErrors.NotFound);
        }

        // Check if user is the creator
        if (eventEntity.CreatedByUserId != userIdInt)
        {
            _logger.LogWarning("User {UserId} attempted to get edit constraints for event {EventId} but is not the creator",
                userIdInt, eventId);
            return Result.Failure<GetEventEditConstraintsResponse>(EventErrors.Unauthorized);
        }

        // Filter active participants (not blocked)
        var activeParticipants = eventEntity.EventParticipants
            .Where(p => p.Status == ParticipantStatus.Accepted)
            .ToList();

        _logger.LogInformation("Event {EventId} has {Count} active participants", eventId, activeParticipants.Count);

        // Calculate age range constraints
        // If there's only 1 participant (the creator), don't apply age constraints
        // This allows the creator to adjust age range using the normal rules (18-99)
        int? minAge = null;
        int? maxAge = null;

        if (activeParticipants.Count > 1)
        {
            var participantAges = activeParticipants
                .Where(p => p.User?.BirthDate != null)
                .Select(p => CalculateAge(p.User!.BirthDate!.Value))
                .ToList();

            minAge = participantAges.Any() ? participantAges.Min() : null;
            maxAge = participantAges.Any() ? participantAges.Max() : null;
        }

        // Analyze gender constraints
        var participantsWithGender = activeParticipants
            .Where(p => p.User?.Gender != null)
            .ToList();

        var participantGenders = participantsWithGender
            .Select(p => p.User!.Gender!.Value)
            .Distinct()
            .ToList();

        // Check if ALL participants have set their gender
        bool allParticipantsHaveGender = activeParticipants.Count > 0 &&
            participantsWithGender.Count == activeParticipants.Count;

        bool hasMixedGenders = participantGenders.Count > 1;
        var allowedRestrictions = CalculateAllowedGenderRestrictions(participantGenders, allParticipantsHaveGender, currentUser.Gender);

        // Check verification status
        bool hasUnverified = activeParticipants
            .Any(p => p.User != null && !p.User.IsVerified);

        // Build warnings
        var warnings = new List<ConstraintWarning>();
        // Only show age warning if there are multiple participants and age constraints exist
        if ((minAge.HasValue || maxAge.HasValue) && activeParticipants.Count > 1)
        {
            warnings.Add(new ConstraintWarning
            {
                Key = "ageRangeLimitedByParticipants",
                Parameters = new Dictionary<string, object>
                {
                    { "minAge", minAge ?? 18 },
                    { "maxAge", maxAge ?? 99 }
                }
            });
        }
        if (hasMixedGenders)
        {
            warnings.Add(new ConstraintWarning
            {
                Key = "genderRestrictionDisabledMixedGenders",
                Parameters = new Dictionary<string, object>()
            });
        }
        else if (!allParticipantsHaveGender && activeParticipants.Count > 0)
        {
            warnings.Add(new ConstraintWarning
            {
                Key = "genderRestrictionDisabledNoGender",
                Parameters = new Dictionary<string, object>()
            });
        }
        if (hasUnverified)
        {
            warnings.Add(new ConstraintWarning
            {
                Key = "cannotRequireVerifiedUsers",
                Parameters = new Dictionary<string, object>()
            });
        }
        // Only show max attendance warning if there are 3+ participants
        // (1-2 participants don't need warning since min max attendance is 2 anyway)
        if (activeParticipants.Count >= 3)
        {
            warnings.Add(new ConstraintWarning
            {
                Key = "maxAttendanceLimitedByParticipants",
                Parameters = new Dictionary<string, object>
                {
                    { "count", activeParticipants.Count }
                }
            });
        }

        var response = new GetEventEditConstraintsResponse
        {
            MinAllowedAge = minAge,
            MaxAllowedAge = maxAge,
            HasMixedGenders = hasMixedGenders,
            AllowedGenderRestrictions = allowedRestrictions,
            MinAllowedMaxAttendance = Math.Max(2, activeParticipants.Count), // Min is 2 or current participants
            HasUnverifiedParticipants = hasUnverified,
            CanRequireVerifiedUsers = !hasUnverified,
            CurrentParticipantCount = activeParticipants.Count,
            Warnings = warnings
        };

        _logger.LogInformation("Edit constraints calculated for event {EventId}: MinAge={MinAge}, MaxAge={MaxAge}, " +
            "MixedGenders={MixedGenders}, MinMaxAttendance={MinMaxAttendance}",
            eventId, minAge, maxAge, hasMixedGenders, activeParticipants.Count);

        return Result.Success(response);
    }

    private static int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.UtcNow;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age))
            age--;
        return age;
    }

    private static List<GenderRestriction> CalculateAllowedGenderRestrictions(
        List<Gender> participantGenders,
        bool allParticipantsHaveGender,
        Gender? userGender)
    {
        // Start with restrictions allowed for the user's gender
        var userAllowedRestrictions = GenderRestrictionExtensions.GetAvailableRestrictions(userGender).ToList();

        // If no participants yet, return only what the user is allowed based on their gender
        if (!participantGenders.Any())
        {
            return userAllowedRestrictions;
        }

        var allowed = new List<GenderRestriction> { GenderRestriction.None };

        // If not ALL participants have set their gender, only allow None (if user can set it)
        if (!allParticipantsHaveGender)
        {
            return allowed.Intersect(userAllowedRestrictions).ToList();
        }

        // If ALL participants have set gender AND all are the same gender, allow that specific restriction
        if (participantGenders.Count == 1)
        {
            var gender = participantGenders.First();
            switch (gender)
            {
                case Gender.Male:
                    allowed.Add(GenderRestriction.MaleOnly);
                    break;
                case Gender.Female:
                    allowed.Add(GenderRestriction.FemaleOnly);
                    break;
                case Gender.NonBinary:
                    allowed.Add(GenderRestriction.NonBinaryOnly);
                    break;
            }
        }
        // If mixed genders (but all have set gender), only allow None

        // Return only restrictions that are allowed for BOTH the user and the participants
        return allowed.Intersect(userAllowedRestrictions).ToList();
    }
}
