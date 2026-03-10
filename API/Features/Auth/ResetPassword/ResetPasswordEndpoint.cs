using API.Common.Extensions;
using FluentValidation;
using Shared.Contracts.Auth;

namespace API.Features.Auth.ResetPassword;

public static class ResetPasswordEndpoint
{
    public static IEndpointRouteBuilder MapResetPassword(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/reset-password", async (
            ResetPasswordRequest request,
            IValidator<ResetPasswordRequest> validator,
            ResetPasswordHandler handler) =>
        {
            // Validate request
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            // Handle reset password
            var result = await handler.Handle(request);

            return result.ToHttpResult();
        })
        .WithName("ResetPassword")
        .WithTags("Auth")
        .AllowAnonymous()
        .Produces<ResetPasswordResponse>(200)
        .Produces(400)
        .Produces(404)
        .Produces(429); // Rate limit

        return app;
    }
}
