using API.Common.Extensions;
using Shared.Contracts.Events;

namespace API.Features.Events.GetMyParticipatingEvents;

public static class GetMyParticipatingEventsEndpoint
{
    public static IEndpointRouteBuilder MapGetMyParticipatingEvents(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events/my-participating", async (
            GetMyParticipatingEventsHandler handler) =>
        {
            var result = await handler.Handle();
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("GetMyParticipatingEvents")
        .WithTags("Events")
        .Produces<List<GetEventDetailsResponse>>(200);

        return app;
    }
}
