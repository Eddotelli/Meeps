using FluentValidation;
using Shared.Contracts.Auth;

namespace API.Features.Auth.ValidateResetToken;

public class ValidateResetTokenValidator : AbstractValidator<ValidateResetTokenRequest>
{
    public ValidateResetTokenValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("Token is required");
    }
}
