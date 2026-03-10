using API.Common.Extensions;
using FluentValidation;
using Shared.Contracts.Messages;

namespace API.Features.Messages.SendMessage;

public static class SendMessageEndpoint
{
    public static IEndpointRouteBuilder MapSendMessage(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/messages", async (
            SendMessageRequest request,
            IValidator<SendMessageRequest> validator,
            SendMessageHandler handler,
            ILogger<SendMessageHandler> logger) =>
        {
            // Log incoming request
            logger.LogInformation("Received message request: EventId={EventId}, TextLength={Length}", 
                request.EventId, request.Text?.Length ?? 0);

            // Validate request
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                logger.LogWarning("Validation failed: {Errors}", 
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                throw new ValidationException(validationResult.Errors);
            }

            // Handle message sending
            var result = await handler.HandleAsync(request);

            // Return appropriate response - 201 Created for successful message
            if (result.IsSuccess)
                return Results.Created(
                    $"/api/messages/{result.Value!.MessageId}",
                    result.Value);

            return result.ToHttpResult();
        })
        .RequireAuthorization()  // Must be logged in
        .WithName("SendMessage")
        .WithTags("Messages")
        .WithOpenApi()
        .Produces<SendMessageResponse>(201)  // Success
        .Produces(400)  // Validation error
        .Produces(401)  // Unauthorized
        .Produces(403)  // Not a participant
        .Produces(404); // Event not found

        return app;
    }
}
