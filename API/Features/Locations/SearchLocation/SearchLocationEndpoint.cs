using API.Common.Extensions;
using Shared.Contracts.Locations;

namespace API.Features.Locations.SearchLocation;

public class SearchLocationEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/locations/search", Handle)
            .WithTags("Locations")
            .WithDescription("Search for locations using Mapbox Geocoding API");
    }

    private static async Task<IResult> Handle(
        string? q,
        int? limit,
        SearchLocationHandler handler)
    {
        var request = new SearchLocationRequest
        {
            Query = q ?? string.Empty,
            Limit = limit ?? 10
        };

        var result = await handler.Handle(request);
        return result.ToHttpResult();
    }
}
