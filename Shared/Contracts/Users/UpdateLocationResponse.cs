namespace Shared.Contracts.Users;

public class UpdateLocationResponse
{
    public string? DefaultCity { get; set; }
    public double? DefaultCityLatitude { get; set; }
    public double? DefaultCityLongitude { get; set; }
    public int SearchRadius { get; set; }
}
