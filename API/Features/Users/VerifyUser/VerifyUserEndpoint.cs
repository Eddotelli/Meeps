using API.Common.Extensions;

namespace API.Features.Users.VerifyUser;

public class VerifyUserEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/verify", Handle)
            .RequireAuthorization()
            .WithTags("Users");
    }

    private static async Task<IResult> Handle(VerifyUserHandler handler)
    {
        var result = await handler.Handle();
        return result.ToHttpResult();
    }
}
