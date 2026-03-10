using System.ComponentModel.DataAnnotations;
using Shared.Enums;

namespace Shared.Contracts.Events;

public class CreateEventRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    [Required]
    public DateTime? DateTime { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? CategoryId { get; set; }

    [Required]
    [Range(2, 20)]
    public int? MinAttendance { get; set; }

    [Required]
    [Range(2, 20)]
    public int? MaxAttendance { get; set; }

    [Range(18, 99)]
    public int? MinAge { get; set; }

    [Range(18, 99)]
    public int? MaxAge { get; set; }

    [Required]
    public GenderRestriction? GenderRestriction { get; set; }

    [Url]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    // For AI-generated images: base64 data sent from client
    public string? Base64Image { get; set; }

    [Required]
    public bool IsPublic { get; set; } = true;

    public bool OnlyVerifiedUsers { get; set; } = false;
}
