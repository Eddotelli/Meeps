using API.Common.Extensions;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdateEmail;

public class UpdateEmailEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/email", Handle)
            .RequireAuthorization()
            .WithTags("Users");
    }

    private static async Task<IResult> Handle(
        UpdateEmailRequest request,
        UpdateEmailHandler handler)
    {
        var result = await handler.Handle(request);
        return result.ToHttpResult();
    }
}
