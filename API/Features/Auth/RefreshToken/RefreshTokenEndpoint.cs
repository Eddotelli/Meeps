using API.Common.Extensions;
using FluentValidation;
using Shared.Contracts.Auth;

namespace API.Features.Auth.RefreshToken;

public static class RefreshTokenEndpoint
{
    public static IEndpointRouteBuilder MapRefreshToken(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/refresh-token", async (
            RefreshTokenHandler handler,
            HttpContext httpContext,
            IConfiguration configuration) =>
        {
            // Get refresh token from cookie
            var refreshToken = httpContext.GetRefreshTokenFromCookie();

            if (string.IsNullOrEmpty(refreshToken))
            {
                return Results.Unauthorized();
            }

            // Handle token refresh
            var result = await handler.HandleAsync(refreshToken);

            if (result.IsFailure)
            {
                return result.ToHttpResult();
            }

            // Set new auth cookies with actual token expiry (respects sliding expiration)
            httpContext.SetAuthCookies(
                result.Value.AccessToken,
                result.Value.NewRefreshToken,
                configuration,
                result.Value.RefreshTokenExpiry);

            // Return response without tokens
            return Results.Ok(result.Value.Response);
        })
        .RequireRateLimiting("token-refresh")
        .WithName("RefreshToken")
        .WithTags("Auth")
        .WithOpenApi()
        .Produces<RefreshTokenResponse>(200)
        .Produces(400)
        .Produces(401);

        return app;
    }
}
