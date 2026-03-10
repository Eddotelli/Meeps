using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Images;

public class GenerateImageRequest
{
    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string Prompt { get; set; } = string.Empty;

    [Required]
    public string Context { get; set; } = string.Empty; // "Profile" or "Event"

    [Range(1, 20)]
    public int? ParticipantCount { get; set; } // Optional, only for Event context (number of people in image)
}
