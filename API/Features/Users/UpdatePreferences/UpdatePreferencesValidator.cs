using FluentValidation;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdatePreferences;

public class UpdatePreferencesValidator : AbstractValidator<UpdatePreferencesRequest>
{
    public UpdatePreferencesValidator()
    {
        RuleFor(x => x.DefaultCity)
            .MaximumLength(100)
            .WithMessage("City name cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.DefaultCity));

        RuleFor(x => x.SearchRadius)
            .InclusiveBetween(5, 100)
            .WithMessage("Search radius must be between 5 and 100 km");
    }
}
