using FluentValidation;
using Shared.Contracts.Auth;

namespace API.Features.Auth.VerifyEmail;

public class VerifyEmailValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty();
    }
}
