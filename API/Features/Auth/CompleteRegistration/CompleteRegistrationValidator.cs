using FluentValidation;
using Shared.Contracts.Auth;

namespace API.Features.Auth.CompleteRegistration;

public class CompleteRegistrationValidator : AbstractValidator<CompleteRegistrationRequest>
{
    public CompleteRegistrationValidator()
    {
        RuleFor(x => x.VerificationToken)
            .NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]")
            .Matches("[a-z]")
            .Matches("[0-9]")
            .Matches("[^a-zA-Z0-9]");

        RuleFor(x => x.AcceptTerms)
            .Equal(true)
            .WithMessage("You must accept the terms to continue");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(50)
            .Matches("^[a-zA-Z0-9_]+$");

        RuleFor(x => x.BirthDate)
            .Must(BeAtLeast18YearsOld)
            .When(x => x.BirthDate.HasValue)
            .WithMessage("You must be at least 18 years old if providing birth date");

        RuleFor(x => x.Gender)
            .IsInEnum()
            .When(x => x.Gender.HasValue)
            .WithMessage("Invalid gender value");

        RuleFor(x => x.CategoryIds)
            .Must(x => x.Length <= 10);
    }

    private static bool BeAtLeast18YearsOld(DateTime? birthDate)
    {
        if (!birthDate.HasValue)
            return true;

        var today = DateTime.Today;
        var age = today.Year - birthDate.Value.Year;

        // Adjust if birthday hasn't occurred yet this year
        if (birthDate.Value.Date > today.AddYears(-age))
        {
            age--;
        }

        return age >= 18;
    }
}
