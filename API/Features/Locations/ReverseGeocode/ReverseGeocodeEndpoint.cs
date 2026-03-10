using API.Common.Extensions;
using Shared.Contracts.Locations;

namespace API.Features.Locations.ReverseGeocode;

public class ReverseGeocodeEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/locations/reverse", Handle)
            .WithTags("Locations")
            .WithDescription("Reverse geocode coordinates to get address details");
    }

    private static async Task<IResult> Handle(
        double lat,
        double lon,
        ReverseGeocodeHandler handler)
    {
        var request = new ReverseGeocodeRequest
        {
            Latitude = lat,
            Longitude = lon
        };

        var result = await handler.Handle(request);
        return result.ToHttpResult();
    }
}
