using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.GetEligibleEvents;

public class GetEligibleEventsValidator : AbstractValidator<GetEligibleEventsRequest>
{
    public GetEligibleEventsValidator()
    {
        RuleFor(x => x.RadiusKm)
            .InclusiveBetween(1, 500)
            .When(x => x.RadiusKm.HasValue)
            .WithMessage("Radius must be between 1 and 500 kilometers");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0)
            .When(x => x.CategoryId.HasValue)
            .WithMessage("Category ID must be greater than 0");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100");

        RuleFor(x => x.SortBy)
            .Must(x => x == "distance" || x == "date" || x == "name" || x == "attendees" || x == "spotsLeft")
            .WithMessage("SortBy must be one of: distance, date, name, attendees, spotsLeft");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.Latitude.HasValue)
            .WithMessage("Latitude must be between -90 and 90");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.Longitude.HasValue)
            .WithMessage("Longitude must be between -180 and 180");

        // If latitude is provided, longitude must also be provided
        RuleFor(x => x.Longitude)
            .NotNull()
            .When(x => x.Latitude.HasValue)
            .WithMessage("Longitude is required when latitude is provided");

        RuleFor(x => x.Latitude)
            .NotNull()
            .When(x => x.Longitude.HasValue)
            .WithMessage("Latitude is required when longitude is provided");
    }
}
