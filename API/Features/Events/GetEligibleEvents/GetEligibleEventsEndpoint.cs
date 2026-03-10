using API.Common.Extensions;
using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.GetEligibleEvents;

public static class GetEligibleEventsEndpoint
{
    public static IEndpointRouteBuilder MapGetEligibleEvents(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events/eligible", async (
            [AsParameters] GetEligibleEventsRequest request,
            IValidator<GetEligibleEventsRequest> validator,
            GetEligibleEventsHandler handler) =>
        {
            // Validate request
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            var result = await handler.Handle(request);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("GetEligibleEvents")
        .WithTags("Events")
        .Produces<GetEligibleEventsResponse>(200);

        return app;
    }
}
