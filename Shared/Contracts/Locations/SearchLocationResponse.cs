namespace Shared.Contracts.Locations;

public class SearchLocationResponse
{
    public List<LocationSearchResult> Results { get; set; } = new();
}
