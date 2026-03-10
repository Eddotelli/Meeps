namespace Shared.Contracts.Locations;

public class ReverseGeocodeResponse
{
    public string DisplayName { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
}
