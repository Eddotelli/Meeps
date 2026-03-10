using API.Common.Extensions;
using FluentValidation;
using Shared.Contracts.Auth;

namespace API.Features.Auth.VerifyEmail;

public static class VerifyEmailEndpoint
{
    public static IEndpointRouteBuilder MapVerifyEmail(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/verify-email", async (
            VerifyEmailRequest request,
            IValidator<VerifyEmailRequest> validator,
            VerifyEmailHandler handler) =>
        {
            // Validate request
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            // Handle email verification
            var result = await handler.HandleAsync(request);

            // Return appropriate response
            return result.ToHttpResult();
        })
        .WithName("VerifyEmail")
        .WithTags("Auth")
        .WithOpenApi()
        .Produces<VerifyEmailResponse>(200)
        .Produces(400);

        return app;
    }
}
