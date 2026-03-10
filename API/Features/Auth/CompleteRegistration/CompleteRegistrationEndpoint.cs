using API.Common.Extensions;
using FluentValidation;
using Shared.Contracts.Auth;

namespace API.Features.Auth.CompleteRegistration;

public static class CompleteRegistrationEndpoint
{
    public static IEndpointRouteBuilder MapCompleteRegistration(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/complete-registration", async (
            CompleteRegistrationRequest request,
            IValidator<CompleteRegistrationRequest> validator,
            CompleteRegistrationHandler handler,
            HttpContext httpContext,
            IConfiguration configuration) =>
        {
            // Validate request
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            // Handle profile completion
            var result = await handler.HandleAsync(request);

            if (result.IsFailure)
            {
                return result.ToHttpResult();
            }

            // Set auth cookies with actual token expiry
            httpContext.SetAuthCookies(
                result.Value.AccessToken,
                result.Value.RefreshToken,
                configuration,
                result.Value.RefreshTokenExpiry);

            // Return response without tokens
            return Results.Ok(result.Value.Response);
        })
        .WithName("CompleteRegistration")
        .WithTags("Auth")
        .WithOpenApi()
        .Produces<CompleteRegistrationResponse>(200)
        .Produces(400);

        return app;
    }
}
