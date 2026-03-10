using FluentValidation;
using Shared.Contracts.Events;
using API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Shared.Extensions;

namespace API.Features.Events.CreateEvent;

public class CreateEventValidator : AbstractValidator<CreateEventRequest>
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CreateEventValidator(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;

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
            .Must(BeAValidUrl)
            .WithMessage("Image URL must be a valid URL")
            .When(x => !string.IsNullOrEmpty(x.ImageUrl));
    }

    private async Task<bool> BeAllowedGenderRestriction(CreateEventRequest request, Shared.Enums.GenderRestriction? restriction, CancellationToken cancellationToken)
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

    private bool BeAValidUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
