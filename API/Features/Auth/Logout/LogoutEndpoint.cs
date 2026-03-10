using API.Common.Extensions;

namespace API.Features.Auth.Logout;

public static class LogoutEndpoint
{
    public static IEndpointRouteBuilder MapLogout(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/logout", async (
            LogoutHandler handler,
            HttpContext httpContext) =>
        {
            // Handle logout
            var result = await handler.HandleAsync(httpContext.User);

            if (result.IsSuccess)
            {
                // Clear auth cookies
                httpContext.ClearAuthCookies();
            }

            // Return appropriate response
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("Logout")
        .WithTags("Auth")
        .WithOpenApi()
        .Produces(200)
        .Produces(401);

        return app;
    }
}
