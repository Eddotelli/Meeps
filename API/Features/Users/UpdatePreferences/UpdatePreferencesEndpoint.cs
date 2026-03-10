using API.Common.Extensions;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdatePreferences;

public class UpdatePreferencesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/preferences", Handle)
            .RequireAuthorization()
            .WithTags("Users");
    }

    private static async Task<IResult> Handle(
        UpdatePreferencesRequest request,
        UpdatePreferencesHandler handler)
    {
        var result = await handler.Handle(request);
        return result.ToHttpResult();
    }
}
