using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Auth;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;
}
