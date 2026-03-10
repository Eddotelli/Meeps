using API.Common.Extensions;
using Shared.Contracts.Events;

namespace API.Features.Events.GetMyEvents;

public static class GetMyEventsEndpoint
{
    public static IEndpointRouteBuilder MapGetMyEvents(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events/my-events", async (
            GetMyEventsHandler handler) =>
        {
            var result = await handler.Handle();
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("GetMyEvents")
        .WithTags("Events")
        .Produces<List<GetEventDetailsResponse>>(200);

        return app;
    }
}
