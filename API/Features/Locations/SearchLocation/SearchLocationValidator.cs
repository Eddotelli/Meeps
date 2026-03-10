using FluentValidation;
using Shared.Contracts.Locations;

namespace API.Features.Locations.SearchLocation;

public class SearchLocationValidator : AbstractValidator<SearchLocationRequest>
{
    public SearchLocationValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(200);

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50);
    }
}
