namespace Shared.Contracts.Auth;

public record LoginResponse(
    string UserId,
    string Email,
    string DisplayName);
