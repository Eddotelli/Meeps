using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Auth;

public class ValidateResetTokenRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}
