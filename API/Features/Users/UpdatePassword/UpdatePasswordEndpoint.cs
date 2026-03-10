using API.Common.Extensions;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdatePassword;

public class UpdatePasswordEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/password", Handle)
            .RequireAuthorization()
            .WithTags("Users");
    }

    private static async Task<IResult> Handle(
        UpdatePasswordRequest request,
        UpdatePasswordHandler handler)
    {
        var result = await handler.Handle(request);
        return result.ToHttpResult();
    }
}
