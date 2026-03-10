using API.Common.Extensions;
using Shared.Contracts.Images;

namespace API.Features.Images.UploadImage;

public class UploadImageEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/images/upload", Handle)
            .RequireAuthorization()
            .DisableAntiforgery() // Required for file uploads
            .WithTags("Images")
            .WithName("UploadImage");
    }

    private static async Task<IResult> Handle(
        IFormFile file,
        string context,
        int? eventId,
        UploadImageHandler handler)
    {
        var result = await handler.HandleAsync(file, context, eventId);
        return result.ToHttpResult();
    }
}
