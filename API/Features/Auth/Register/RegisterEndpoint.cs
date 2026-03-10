using API.Common.Extensions;
using FluentValidation;
using Shared.Contracts.Auth;

namespace API.Features.Auth.Register;

public static class RegisterEndpoint
{
    public static IEndpointRouteBuilder MapRegister(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", async (
            RegisterRequest request,
            IValidator<RegisterRequest> validator,
            RegisterHandler handler) =>
        {
            // Validate request
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            // Handle registration
            var result = await handler.HandleAsync(request);

            // Return appropriate response
            return result.ToHttpResult();
        })
        .RequireRateLimiting("auth")
        .WithName("Register")
        .WithTags("Auth")
        .WithOpenApi()
        .Produces<RegisterResponse>(200)
        .Produces(400);

        return app;
    }
}
