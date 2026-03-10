using Shared.Common.Results;
using Shared.Contracts.Locations;

namespace Client.Services.ApiClients;

public interface ILocationsApiClient
{
    Task<Result<SearchLocationResponse>> SearchLocationAsync(string query, int limit = 10);
    Task<Result<ReverseGeocodeResponse>> ReverseGeocodeAsync(double latitude, double longitude);
}
