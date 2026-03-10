using API.Common.Extensions;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdateLocation;

public class UpdateLocationEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/location", Handle)
            .RequireAuthorization()
            .WithTags("Users")
            .WithDescription("Update user's default location and search radius");
    }

    private static async Task<IResult> Handle(
        UpdateLocationRequest request,
        UpdateLocationHandler handler)
    {
        var result = await handler.Handle(request);
        return result.ToHttpResult();
    }
}
