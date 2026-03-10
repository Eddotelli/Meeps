namespace Shared.Contracts.Users;

public class GetUserPreferencesResponse
{
    public string? DefaultCity { get; set; }
    public double? DefaultCityLatitude { get; set; }
    public double? DefaultCityLongitude { get; set; }
    public int SearchRadius { get; set; }
    public int[] CategoryIds { get; set; } = Array.Empty<int>();
}
