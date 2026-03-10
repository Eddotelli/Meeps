using FluentValidation;
using Shared.Contracts.Locations;

namespace API.Features.Locations.ReverseGeocode;

public class ReverseGeocodeValidator : AbstractValidator<ReverseGeocodeRequest>
{
    public ReverseGeocodeValidator()
    {
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Latitude must be between -90 and 90");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Longitude must be between -180 and 180");
    }
}
