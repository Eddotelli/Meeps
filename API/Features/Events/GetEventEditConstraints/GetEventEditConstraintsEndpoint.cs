using API.Common.Extensions;
using Shared.Contracts.Events;

namespace API.Features.Events.GetEventEditConstraints;

public static class GetEventEditConstraintsEndpoint
{
    public static IEndpointRouteBuilder MapGetEventEditConstraints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events/{eventId}/edit-constraints", Handle)
            .RequireAuthorization()
            .Produces<GetEventEditConstraintsResponse>(200)
            .WithName("GetEventEditConstraints")
            .WithOpenApi()
            .WithTags("Events");

        return app;
    }

    private static async Task<IResult> Handle(
        int eventId,
        GetEventEditConstraintsHandler handler)
    {
        var result = await handler.Handle(eventId);
        return result.ToHttpResult();
    }
}
