using Shared.Contracts.Auth;
using API.Common.Extensions;
using FluentValidation;

namespace API.Features.Auth.ValidateResetToken;

public static class ValidateResetTokenEndpoint
{
    public static IEndpointRouteBuilder MapValidateResetToken(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/validate-reset-token", async (
            ValidateResetTokenRequest request,
            IValidator<ValidateResetTokenRequest> validator,
            ValidateResetTokenHandler handler) =>
        {
            // Validate request
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            // Handle token validation
            var result = await handler.Handle(request);

            return result.ToHttpResult();
        })
        .WithName("ValidateResetToken")
        .WithTags("Auth")
        .AllowAnonymous()
        .RequireRateLimiting("auth");

        return app;
    }
}
