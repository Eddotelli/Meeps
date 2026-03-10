using FluentValidation;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdateEmail;

public class UpdateEmailValidator : AbstractValidator<UpdateEmailRequest>
{
    public UpdateEmailValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Invalid email address")
            .MaximumLength(256)
            .WithMessage("Email cannot exceed 256 characters");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required for email change");
    }
}
