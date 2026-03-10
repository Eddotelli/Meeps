using API.Common.Extensions;
using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.CreateEvent;

public static class CreateEventEndpoint
{
    public static IEndpointRouteBuilder MapCreateEvent(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/events", async (
            CreateEventRequest request,
            IValidator<CreateEventRequest> validator,
            CreateEventHandler handler) =>
        {
            // Validate request
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            // Handle event creation
            var result = await handler.HandleAsync(request);

            // Return appropriate response - 201 Created for successful creation
            if (result.IsSuccess)
                return Results.Created($"/api/events/{result.Value!.EventId}", result.Value);
            
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("CreateEvent")
        .WithTags("Events")
        .WithOpenApi()
        .Produces<CreateEventResponse>(201)
        .Produces(400)
        .Produces(401)
        .Produces(404);

        return app;
    }
}
