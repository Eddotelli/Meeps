namespace Shared.Contracts.Events;

using Shared.Enums;

public class GetNearbyEventsResponse
{
    public List<NearbyEventDto> Events { get; set; } = new();
}

public class NearbyEventDto
{
    public int Id { get; set; }
    public string EventHash { get; set; } = string.Empty; // Hash for use in URLs
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime DateTime { get; set; }
    public CategoryType Category { get; set; }
    public int CurrentAttendance { get; set; }
    public int MaxAttendance { get; set; }
    public double DistanceKm { get; set; }
    public string CreatorUsername { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool OnlyVerifiedUsers { get; set; }
}
