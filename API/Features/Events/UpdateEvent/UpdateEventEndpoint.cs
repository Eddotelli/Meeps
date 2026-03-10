using API.Common.Extensions;
using API.Infrastructure.Services;
using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.UpdateEvent;

public static class UpdateEventEndpoint
{
    public static IEndpointRouteBuilder MapUpdateEvent(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/events/{hash}", async (
            string hash,
            IHashIdService hashIdService,
            UpdateEventRequest request,
            IValidator<UpdateEventRequest> validator,
            UpdateEventHandler handler) =>
        {
            var id = hashIdService.Decode(hash);
            if (id == null)
            {
                return Results.NotFound();
            }

            // Ensure EventId matches route parameter
            request.EventId = id.Value;

            // Validate request
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            var result = await handler.HandleAsync(request);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("UpdateEvent")
        .WithTags("Events")
        .Produces<UpdateEventResponse>(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404);

        return app;
    }
}
