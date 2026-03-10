using API.Infrastructure.Services;
using Shared.Common.Results;
using Shared.Contracts.Locations;

namespace API.Features.Locations.SearchLocation;

public class SearchLocationHandler
{
    private readonly IMapboxService _mapboxService;
    private readonly ILogger<SearchLocationHandler> _logger;

    public SearchLocationHandler(
        IMapboxService mapboxService,
        ILogger<SearchLocationHandler> logger)
    {
        _mapboxService = mapboxService;
        _logger = logger;
    }

    public async Task<Result<SearchLocationResponse>> Handle(SearchLocationRequest request)
    {
        var result = await _mapboxService.SearchLocationAsync(request.Query, request.Limit);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Location search successful via Mapbox for query: '{Query}'", request.Query);
        }
        else
        {
            _logger.LogWarning("Mapbox search failed for query: '{Query}'. Error: {Error}",
                request.Query, result.Error?.Code);
        }

        return result;
    }
}
