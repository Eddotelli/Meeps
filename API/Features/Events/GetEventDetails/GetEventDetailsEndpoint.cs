using API.Common.Extensions;
using API.Infrastructure.Services;
using Shared.Contracts.Events;

namespace API.Features.Events.GetEventDetails;

public static class GetEventDetailsEndpoint
{
    public static IEndpointRouteBuilder MapGetEventDetails(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events/{hash}", async (
            string hash,
            IHashIdService hashIdService,
            GetEventDetailsHandler handler) =>
        {
            var id = hashIdService.Decode(hash);
            if (id == null)
            {
                return Results.NotFound();
            }

            var result = await handler.Handle(id.Value);
            return result.ToHttpResult();
        })
        .WithName("GetEventDetails")
        .WithTags("Events")
        .Produces<GetEventDetailsResponse>(200)
        .Produces(404);

        return app;
    }
}
