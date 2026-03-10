namespace Shared.Contracts.Events;

using Shared.Enums;

public class GetEligibleEventsResponse
{
    public List<EligibleEventDto> Events { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public double? SearchLatitude { get; set; }
    public double? SearchLongitude { get; set; }
    public int SearchRadiusKm { get; set; }
}

public class EligibleEventDto
{
    public int Id { get; set; }
    public string EventHash { get; set; } = string.Empty; // Hash for use in URLs
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime DateTime { get; set; }
    public DateTime CreatedAt { get; set; }

    public int CategoryId { get; set; }
    public CategoryType Category { get; set; }

    public int MinAttendance { get; set; }
    public int MaxAttendance { get; set; }
    public int CurrentAttendance { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public GenderRestriction GenderRestriction { get; set; }
    public string? ImageUrl { get; set; }
    public bool OnlyVerifiedUsers { get; set; }

    /// <summary>
    /// Distance from search location in kilometers.
    /// </summary>
    public double? DistanceKm { get; set; }

    public bool IsUserParticipant { get; set; }
    public bool IsUserCreator { get; set; }
}
