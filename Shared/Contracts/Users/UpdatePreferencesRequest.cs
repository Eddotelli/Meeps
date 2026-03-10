using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Users;

public class UpdatePreferencesRequest
{
    [StringLength(100)]
    public string? DefaultCity { get; set; }

    [Range(-90, 90)]
    public double? DefaultCityLatitude { get; set; }

    [Range(-180, 180)]
    public double? DefaultCityLongitude { get; set; }

    [Range(1, 100)]
    public int SearchRadius { get; set; } = 25;

    public int[] CategoryIds { get; set; } = Array.Empty<int>();
}
