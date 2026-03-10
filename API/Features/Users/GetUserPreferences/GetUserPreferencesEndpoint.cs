using API.Common.Extensions;

namespace API.Features.Users.GetUserPreferences;

public class GetUserPreferencesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users/preferences", Handle)
            .RequireAuthorization()
            .WithTags("Users");
    }

    private static async Task<IResult> Handle(GetUserPreferencesHandler handler)
    {
        var result = await handler.Handle();
        return result.ToHttpResult();
    }
}
