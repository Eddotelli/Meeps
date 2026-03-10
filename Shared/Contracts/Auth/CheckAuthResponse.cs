namespace Shared.Contracts.Auth;

/// <summary>
/// Response for checking authentication status.
/// </summary>
public class CheckAuthResponse
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}
