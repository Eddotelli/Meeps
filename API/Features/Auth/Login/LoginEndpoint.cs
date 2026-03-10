using API.Common.Extensions;
using FluentValidation;
using Shared.Contracts.Auth;

namespace API.Features.Auth.Login;

public static class LoginEndpoint
{
    public static IEndpointRouteBuilder MapLogin(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            IValidator<LoginRequest> validator,
            LoginHandler handler,
            HttpContext httpContext,
            IConfiguration configuration) =>
        {
            // Validate request
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            // Handle login
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
        .RequireRateLimiting("auth")
        .WithName("Login")
        .WithTags("Auth")
        .WithOpenApi()
        .Produces<LoginResponse>(200)
        .Produces(400)
        .Produces(401);

        return app;
    }
}
