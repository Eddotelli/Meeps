namespace Shared.Contracts.Locations;

public class LocationSearchResult
{
    public string DisplayName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
}
