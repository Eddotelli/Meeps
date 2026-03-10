using System.ComponentModel.DataAnnotations;
using Shared.Common.Validation;
using Shared.Enums;

namespace Shared.Contracts.Users;

public class UpdateProfileRequest
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    [RegularExpression(@"^[a-zA-Z0-9_]+$")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Bio { get; set; }

    public Gender? Gender { get; set; }

    [MinimumAgeFromDate(18)]
    public DateTime? BirthDate { get; set; }

    // For AI-generated profile images: base64 data sent from client
    public string? Base64Image { get; set; }
}
