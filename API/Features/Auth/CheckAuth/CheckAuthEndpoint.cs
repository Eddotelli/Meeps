using API.Common.Extensions;
using Shared.Common.Results;
using Shared.Contracts.Auth;

namespace API.Features.Auth.CheckAuth;

/// <summary>
/// Endpoint for checking if user is authenticated (has valid JWT cookie).
/// </summary>
public class CheckAuthEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/check", Handle)
            .RequireAuthorization()
            .RequireRateLimiting("api")
            .WithTags("Auth")
            .WithName("CheckAuth");
    }

    private static async Task<IResult> Handle(CheckAuthHandler handler)
    {
        var result = await handler.Handle();
        return result.ToHttpResult();
    }
}
