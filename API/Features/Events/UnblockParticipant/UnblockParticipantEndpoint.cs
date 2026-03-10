using API.Common.Extensions;
using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.UnblockParticipant;

public static class UnblockParticipantEndpoint
{
    public static IEndpointRouteBuilder MapUnblockParticipant(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/events/{eventId}/participants/{userId}/block", async (
            int eventId,
            int userId,
            IValidator<UnblockParticipantRequest> validator,
            UnblockParticipantHandler handler) =>
        {
            var request = new UnblockParticipantRequest
            {
                EventId = eventId,
                UserId = userId
            };

            // Validate request
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            // Handle unblocking participant
            var result = await handler.HandleAsync(request);

            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("UnblockParticipant")
        .WithTags("Events")
        .WithOpenApi()
        .Produces<UnblockParticipantResponse>(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(404);

        return app;
    }
}
