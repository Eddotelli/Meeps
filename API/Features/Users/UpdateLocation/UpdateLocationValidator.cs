using FluentValidation;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdateLocation;

public class UpdateLocationValidator : AbstractValidator<UpdateLocationRequest>
{
    public UpdateLocationValidator()
    {
        RuleFor(x => x.DefaultCityLatitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.DefaultCityLatitude.HasValue);

        RuleFor(x => x.DefaultCityLongitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.DefaultCityLongitude.HasValue);

        RuleFor(x => x.SearchRadius)
            .InclusiveBetween(1, 100);
    }
}
