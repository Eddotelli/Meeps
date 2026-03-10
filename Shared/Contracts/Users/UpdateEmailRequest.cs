using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Users;

public class UpdateEmailRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string NewEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}
