using API.Common.Extensions;
using API.Infrastructure.Services;
using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.LeaveEvent;

public static class LeaveEventEndpoint
{
    public static IEndpointRouteBuilder MapLeaveEvent(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/events/{eventHash}/leave", async (
            string eventHash,
            IHashIdService hashIdService,
            IValidator<LeaveEventRequest> validator,
            LeaveEventHandler handler) =>
        {
            var eventId = hashIdService.Decode(eventHash);
            if (eventId == null)
            {
                return Results.NotFound();
            }

            var request = new LeaveEventRequest { EventId = eventId.Value };

            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var result = await handler.HandleAsync(request);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("LeaveEvent")
        .WithTags("Events")
        .WithOpenApi()
        .Produces<LeaveEventResponse>(200)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .Produces(409);

        return app;
    }
}
