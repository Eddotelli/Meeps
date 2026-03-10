using API.Common.Extensions;
using Shared.Contracts.Users;

namespace API.Features.Users.GetProfileEditConstraints;

public class GetProfileEditConstraintsEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users/profile/edit-constraints", Handle)
            .RequireAuthorization()
            .WithTags("Users");
    }

    private static async Task<IResult> Handle(GetProfileEditConstraintsHandler handler)
    {
        var result = await handler.Handle();
        return result.ToHttpResult();
    }
}
