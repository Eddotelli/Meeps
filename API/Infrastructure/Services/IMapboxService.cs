using Shared.Common.Results;
using Shared.Contracts.Locations;

namespace API.Infrastructure.Services;

public interface IMapboxService
{
    Task<Result<SearchLocationResponse>> SearchLocationAsync(string query, int limit = 10);
    Task<Result<ReverseGeocodeResponse>> ReverseGeocodeAsync(double latitude, double longitude);
}
