using FluentValidation;
using Shared.Contracts.Events;
using API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;
using System.Security.Claims;
using Shared.Extensions;

namespace API.Features.Events.UpdateEvent;

public class UpdateEventValidator : AbstractValidator<UpdateEventRequest>
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UpdateEventValidator(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;

        RuleFor(x => x.EventId)
            .GreaterThan(0);

        RuleFor(x => x.Title)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(1000);

        RuleFor(x => x.Location)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DateTime)
            .NotNull()
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Event date must be in the future");

        RuleFor(x => x.CategoryId)
            .NotNull()
            .GreaterThan(0)
            .WithMessage("Please select a valid category");

        RuleFor(x => x.MinAttendance)
            .NotNull()
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(20)
            .WithMessage("Minimum attendance must be between 1 and 20");

        RuleFor(x => x.MaxAttendance)
            .NotNull()
            .GreaterThanOrEqualTo(2)
            .LessThanOrEqualTo(20)
            .GreaterThanOrEqualTo(x => x.MinAttendance ?? 2)
            .WithMessage("Max attendance must be greater than or equal to min attendance and between 2 and 20");

        RuleFor(x => x.MinAge)
            .InclusiveBetween(18, 99)
            .When(x => x.MinAge.HasValue)
            .WithMessage("Minimum age must be between 18 and 99");

        RuleFor(x => x.MaxAge)
            .InclusiveBetween(18, 99)
            .When(x => x.MaxAge.HasValue)
            .GreaterThanOrEqualTo(x => x.MinAge ?? 18)
            .When(x => x.MaxAge.HasValue && x.MinAge.HasValue)
            .WithMessage("Max age must be greater than or equal to min age");

        RuleFor(x => x.GenderRestriction)
            .NotNull()
            .IsInEnum()
            .MustAsync(BeAllowedGenderRestriction)
            .WithMessage("You can only create events for your own gender group or for everyone");

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.ImageUrl));

        // Custom validation against existing participants
        RuleFor(x => x)
            .MustAsync(ValidateAgainstParticipants)
            .WithMessage("Event changes would exclude existing participants or violate participation constraints");
    }

    private async Task<bool> ValidateAgainstParticipants(
        UpdateEventRequest request,
        CancellationToken cancellation)
    {
        var eventEntity = await _context.Events
            .Include(e => e.EventParticipants)
                .ThenInclude(ep => ep.User)
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellation);

        if (eventEntity == null) return true; // Event not found, will be handled elsewhere

        var activeParticipants = eventEntity.EventParticipants
            .Where(p => p.Status == ParticipantStatus.Accepted)
            .ToList();

        // If only 1 participant (the creator), no constraints apply
        // This allows normal age range adjustments (18-99) like when creating an event
        if (activeParticipants.Count <= 1) return true;

        // 1. Validate age range doesn't exclude existing participants
        if (request.MinAge.HasValue || request.MaxAge.HasValue)
        {
            var participantAges = activeParticipants
                .Where(p => p.User?.BirthDate != null)
                .Select(p => CalculateAge(p.User!.BirthDate!.Value))
                .ToList();

            if (participantAges.Any())
            {
                int minParticipantAge = participantAges.Min();
                int maxParticipantAge = participantAges.Max();

                if ((request.MinAge.HasValue && request.MinAge.Value > minParticipantAge) ||
                    (request.MaxAge.HasValue && request.MaxAge.Value < maxParticipantAge))
                {
                    return false; // Would exclude participant(s) based on age
                }
            }
        }

        // 2. Validate gender restriction doesn't exclude existing participants
        var participantsWithGender = activeParticipants
            .Where(p => p.User?.Gender != null)
            .ToList();

        var participantGenders = participantsWithGender
            .Select(p => p.User!.Gender!.Value)
            .Distinct()
            .ToList();

        if (activeParticipants.Any() && request.GenderRestriction.HasValue && request.GenderRestriction.Value != GenderRestriction.None)
        {
            // If not all participants have set their gender, cannot apply restriction
            if (participantsWithGender.Count < activeParticipants.Count)
            {
                return false; // Some participants haven't set gender
            }

            // Check if restriction would exclude participants
            bool wouldExclude = request.GenderRestriction.Value switch
            {
                GenderRestriction.MaleOnly => participantGenders.Any(g => g != Gender.Male),
                GenderRestriction.FemaleOnly => participantGenders.Any(g => g != Gender.Female),
                GenderRestriction.NonBinaryOnly => participantGenders.Any(g => g != Gender.NonBinary),
                _ => false
            };

            if (wouldExclude) return false; // Would exclude participant(s) based on gender
        }

        // 3. Validate MaxAttendance isn't lower than current participant count
        if (request.MaxAttendance.HasValue && request.MaxAttendance.Value < activeParticipants.Count)
        {
            return false; // Would exceed max attendance with current participants
        }

        // 4. Validate OnlyVerifiedUsers requirement
        if (request.OnlyVerifiedUsers)
        {
            bool hasUnverified = activeParticipants
                .Any(p => p.User != null && !p.User.IsVerified);

            if (hasUnverified) return false; // Has unverified participants
        }

        return true;
    }

    private async Task<bool> BeAllowedGenderRestriction(UpdateEventRequest request, Shared.Enums.GenderRestriction? restriction, CancellationToken cancellationToken)
    {
        if (!restriction.HasValue)
            return false;

        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return false;

        var user = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.Gender })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
            return false;

        // Check if user can set this restriction based on their gender
        return GenderRestrictionExtensions.CanUserSetRestriction(user.Gender, restriction.Value);
    }

    private static int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.UtcNow;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age))
            age--;
        return age;
    }
}
