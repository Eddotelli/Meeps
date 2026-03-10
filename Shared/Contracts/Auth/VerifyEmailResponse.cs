namespace Shared.Contracts.Auth;

public record VerifyEmailResponse(
    string Message,
    string UserId,
    string Email);
