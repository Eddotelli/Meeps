using API.Common.Extensions;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdateCategories;

public class UpdateCategoriesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/categories", Handle)
            .RequireAuthorization()
            .WithTags("Users")
            .WithDescription("Update user's event category preferences");
    }

    private static async Task<IResult> Handle(
        UpdateCategoriesRequest request,
        UpdateCategoriesHandler handler)
    {
        var result = await handler.Handle(request);
        return result.ToHttpResult();
    }
}
