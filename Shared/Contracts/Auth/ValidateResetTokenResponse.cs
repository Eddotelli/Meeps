namespace Shared.Contracts.Auth;

public class ValidateResetTokenResponse
{
    public bool IsValid { get; set; }
    public string? ErrorCode { get; set; }
}
