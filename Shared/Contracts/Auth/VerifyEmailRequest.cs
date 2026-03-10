namespace Shared.Contracts.Auth;

public record VerifyEmailRequest(string Token, string? UserId = null);
