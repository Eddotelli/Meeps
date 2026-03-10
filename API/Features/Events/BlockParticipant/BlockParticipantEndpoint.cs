using API.Common.Extensions;
using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.BlockParticipant;

public static class BlockParticipantEndpoint
{
    public static IEndpointRouteBuilder MapBlockParticipant(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/events/{eventId}/participants/{userId}/block", async (
            int eventId,
            int userId,
            BlockParticipantRequest bodyRequest,
            IValidator<BlockParticipantRequest> validator,
            BlockParticipantHandler handler) =>
        {
            var request = new BlockParticipantRequest
            {
                EventId = eventId,
                UserId = userId,
                Reason = bodyRequest.Reason
            };

            // Validate request
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            // Handle blocking participant
            var result = await handler.HandleAsync(request);

            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("BlockParticipant")
        .WithTags("Events")
        .WithOpenApi()
        .Produces<BlockParticipantResponse>(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404);

        return app;
    }
}
