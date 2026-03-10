using API.Common.Extensions;
using FluentValidation;
using Shared.Contracts.Auth;

namespace API.Features.Auth.ForgotPassword;

public static class ForgotPasswordEndpoint
{
    public static IEndpointRouteBuilder MapForgotPassword(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/forgot-password", async (
            ForgotPasswordRequest request,
            IValidator<ForgotPasswordRequest> validator,
            ForgotPasswordHandler handler) =>
        {
            // Validate request
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            // Handle forgot password
            var result = await handler.Handle(request);

            return result.ToHttpResult();
        })
        .WithName("ForgotPassword")
        .WithTags("Auth")
        .AllowAnonymous()
        .Produces<ForgotPasswordResponse>(200)
        .Produces(400)
        .Produces(429); // Rate limit

        return app;
    }
}
