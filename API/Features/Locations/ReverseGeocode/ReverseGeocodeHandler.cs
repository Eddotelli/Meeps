using API.Infrastructure.Services;
using Shared.Common.Results;
using Shared.Contracts.Locations;

namespace API.Features.Locations.ReverseGeocode;

public class ReverseGeocodeHandler
{
    private readonly IMapboxService _mapboxService;
    private readonly ILogger<ReverseGeocodeHandler> _logger;

    public ReverseGeocodeHandler(
        IMapboxService mapboxService,
        ILogger<ReverseGeocodeHandler> logger)
    {
        _mapboxService = mapboxService;
        _logger = logger;
    }

    public async Task<Result<ReverseGeocodeResponse>> Handle(ReverseGeocodeRequest request)
    {
        var result = await _mapboxService.ReverseGeocodeAsync(request.Latitude, request.Longitude);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Reverse geocode successful via Mapbox for: {Lat},{Lon}", request.Latitude, request.Longitude);
        }
        else
        {
            _logger.LogWarning("Mapbox reverse geocode failed for: {Lat},{Lon}. Error: {Error}",
                request.Latitude, request.Longitude, result.Error?.Code);
        }

        return result;
    }
}
