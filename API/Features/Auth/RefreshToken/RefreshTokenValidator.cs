using FluentValidation;
using Shared.Contracts.Auth;

namespace API.Features.Auth.RefreshToken;

public class RefreshTokenValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenValidator()
    {
        // No validation needed since token comes from cookie
    }
}
