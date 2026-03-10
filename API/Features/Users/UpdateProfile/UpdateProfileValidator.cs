using FluentValidation;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdateProfile;

public class UpdateProfileValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("Display name is required")
            .MinimumLength(2)
            .WithMessage("Display name must be at least 2 characters")
            .MaximumLength(50)
            .WithMessage("Display name cannot exceed 50 characters")
            .Matches(@"^[a-zA-Z0-9_]+$")
            .WithMessage("Display name can only contain letters, numbers and underscores");

        RuleFor(x => x.Bio)
            .MaximumLength(500)
            .WithMessage("Bio cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Bio));

        RuleFor(x => x.Gender)
            .IsInEnum()
            .When(x => x.Gender.HasValue)
            .WithMessage("Invalid gender value");

        RuleFor(x => x.BirthDate)
            .Must(BeAtLeast18YearsOld)
            .WithMessage("You must be at least 18 years old")
            .When(x => x.BirthDate.HasValue);
    }

    private bool BeAtLeast18YearsOld(DateTime? birthDate)
    {
        if (!birthDate.HasValue) return true;
        var age = DateTime.Today.Year - birthDate.Value.Year;
        if (birthDate.Value.Date > DateTime.Today.AddYears(-age)) age--;
        return age >= 18;
    }
}

