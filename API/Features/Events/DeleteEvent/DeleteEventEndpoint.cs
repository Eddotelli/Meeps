using API.Common.Extensions;
using API.Infrastructure.Services;
using Shared.Contracts.Events;

namespace API.Features.Events.DeleteEvent;

public static class DeleteEventEndpoint
{
    public static IEndpointRouteBuilder MapDeleteEvent(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/events/{hash}", async (
            string hash,
            IHashIdService hashIdService,
            DeleteEventHandler handler) =>
        {
            var id = hashIdService.Decode(hash);
            if (id == null)
            {
                return Results.NotFound();
            }

            var result = await handler.HandleAsync(id.Value);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithTags("Events")
        .WithName("DeleteEvent")
        .Produces<DeleteEventResponse>(200)
        .Produces(401)
        .Produces(403)
        .Produces(404);

        return app;
    }
}
