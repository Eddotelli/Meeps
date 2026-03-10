using System.ComponentModel.DataAnnotations;
using Shared.Common.Validation;
using Shared.Enums;

namespace Shared.Contracts.Auth;

public class CompleteRegistrationRequest
{
    public string VerificationToken { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 2)]
    [RegularExpression(@"^[a-zA-Z0-9_]+$")]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$")]
    public string Password { get; set; } = string.Empty;

    [MustBeTrue]
    public bool AcceptTerms { get; set; } = false;

    [MinimumAgeFromDate(18)]
    public DateTime? BirthDate { get; set; }

    public Gender? Gender { get; set; }

    public int[] CategoryIds { get; set; } = Array.Empty<int>();
}
