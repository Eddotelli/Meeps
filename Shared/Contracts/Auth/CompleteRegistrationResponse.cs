namespace Shared.Contracts.Auth;

public record CompleteRegistrationResponse(
    string UserId,
    string Email,
    string DisplayName);
