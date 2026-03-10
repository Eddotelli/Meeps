using API.Common.Extensions;
using Shared.Contracts.Messages;

namespace API.Features.Messages.GetEventMessages;

public static class GetEventMessagesEndpoint
{
    public static IEndpointRouteBuilder MapGetEventMessages(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/messages/{eventHash}", async (
            string eventHash,
            int pageNumber,
            int pageSize,
            GetEventMessagesHandler handler) =>
        {
            var request = new GetEventMessagesRequest
            {
                EventHash = eventHash,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await handler.HandleAsync(request);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("GetEventMessages")
        .WithTags("Messages")
        .WithOpenApi()
        .Produces<GetEventMessagesResponse>(200)
        .Produces(400)  // Validation error
        .Produces(401)  // Unauthorized
        .Produces(403)  // Not a participant
        .Produces(404); // Event not found

        return app;
    }
}
