using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Events;

public class GetEligibleEventsRequest
{
    /// <summary>
    /// Latitude for location-based filtering. Uses user's default location if not provided.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Longitude for location-based filtering. Uses user's default location if not provided.
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Search radius in kilometers. Uses user's default search radius if not provided.
    /// </summary>
    [Range(1, 500)]
    public int? RadiusKm { get; set; }

    /// <summary>
    /// Optional category filter.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? CategoryId { get; set; }

    /// <summary>
    /// Filter events starting after this date. Defaults to current date/time.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Sort by: "distance", "date", "name", "attendees", "spotsLeft". Default is "distance".
    /// </summary>
    [RegularExpression("^(distance|date|name|attendees|spotsLeft)$")]
    public string SortBy { get; set; } = "distance";

    /// <summary>
    /// Page number for pagination (1-based).
    /// </summary>
    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Number of events per page.
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
