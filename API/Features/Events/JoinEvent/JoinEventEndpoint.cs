using API.Common.Extensions;
using API.Infrastructure.Services;
using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.JoinEvent;

public static class JoinEventEndpoint
{
    public static IEndpointRouteBuilder MapJoinEvent(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/events/{eventHash}/join", async (
            string eventHash,
            IHashIdService hashIdService,
            IValidator<JoinEventRequest> validator,
            JoinEventHandler handler) =>
        {
            var eventId = hashIdService.Decode(eventHash);
            if (eventId == null)
            {
                return Results.NotFound();
            }

            var request = new JoinEventRequest { EventId = eventId.Value };

            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var result = await handler.HandleAsync(request);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("JoinEvent")
        .WithTags("Events")
        .WithOpenApi()
        .Produces<JoinEventResponse>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404);

        return app;
    }
}
