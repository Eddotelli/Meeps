using API.Common.Extensions;
using Shared.Common.Results;
using Shared.Contracts.Users;

namespace API.Features.Users.GetUserProfile;

/// <summary>
/// Endpoint for getting the current user's profile information.
/// </summary>
public class GetUserProfileEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users/me", Handle)
            .RequireAuthorization()
            .WithTags("Users")
            .WithName("GetUserProfile");
    }

    private static async Task<IResult> Handle(GetUserProfileHandler handler)
    {
        var result = await handler.Handle();
        return result.ToHttpResult();
    }
}
