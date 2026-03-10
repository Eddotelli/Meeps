using Shared.Common.Results;
using Shared.Contracts.Locations;

namespace Client.Services.ApiClients;

public class LocationsApiClient : ILocationsApiClient
{
    private readonly ApiClient _apiClient;

    public LocationsApiClient(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<Result<SearchLocationResponse>> SearchLocationAsync(string query, int limit = 10)
    {
        return await _apiClient.GetAsync<SearchLocationResponse>($"/api/locations/search?q={Uri.EscapeDataString(query)}&limit={limit}");
    }

    public async Task<Result<ReverseGeocodeResponse>> ReverseGeocodeAsync(double latitude, double longitude)
    {
        return await _apiClient.GetAsync<ReverseGeocodeResponse>($"/api/locations/reverse?lat={latitude}&lon={longitude}");
    }
}
