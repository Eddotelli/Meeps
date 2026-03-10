using API.Common.Extensions;
using Shared.Contracts.Events;

namespace API.Features.Events.GetMyArchivedEvents;

public static class GetMyArchivedEventsEndpoint
{
    public static IEndpointRouteBuilder MapGetMyArchivedEvents(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events/my-archived", async (
            GetMyArchivedEventsHandler handler) =>
        {
            var result = await handler.Handle();
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("GetMyArchivedEvents")
        .WithTags("Events")
        .Produces<List<GetEventDetailsResponse>>(200);

        return app;
    }
}
