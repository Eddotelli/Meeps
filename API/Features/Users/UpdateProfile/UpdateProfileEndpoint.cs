using API.Common.Extensions;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdateProfile;

public class UpdateProfileEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/me", Handle)
            .RequireAuthorization()
            .WithTags("Users");
    }

    private static async Task<IResult> Handle(
        UpdateProfileRequest request,
        UpdateProfileHandler handler)
    {
        var result = await handler.Handle(request);
        return result.ToHttpResult();
    }
}
