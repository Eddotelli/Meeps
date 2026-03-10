using API.Common.Extensions;
using Shared.Contracts.Users;

namespace API.Features.Users.DeleteAccount;

public class DeleteAccountEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/account/delete", Handle)
            .RequireAuthorization()
            .WithTags("Users")
            .WithName("DeleteAccount")
            .Produces<DeleteAccountResponse>(200)
            .Produces(400)
            .Produces(401);
    }

    private static async Task<IResult> Handle(
        DeleteAccountRequest request,
        DeleteAccountHandler handler)
    {
        var result = await handler.Handle(request);
        return result.ToHttpResult();
    }
}
