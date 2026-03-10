using API.Common.Extensions;
using Shared.Contracts.Images;

namespace API.Features.Images.GenerateImage;

public class GenerateImageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/images/generate", Handle)
            .RequireAuthorization()
            .WithTags("Images")
            .WithName("GenerateImage");
    }

    private static async Task<IResult> Handle(
        GenerateImageRequest request,
        GenerateImageHandler handler)
    {
        var result = await handler.HandleAsync(request);
        return result.ToHttpResult();
    }
}
