using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Locations;

namespace API.Infrastructure.Services;

public class MapboxService : IMapboxService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MapboxService> _logger;
    private readonly string _accessToken;
    private const string BaseUrl = "https://api.mapbox.com/geocoding/v5/mapbox.places";

    public MapboxService(
        HttpClient httpClient,
        ILogger<MapboxService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _accessToken = configuration["Mapbox:AccessToken"]
            ?? throw new InvalidOperationException("Mapbox:AccessToken not configured");
    }

    public async Task<Result<SearchLocationResponse>> SearchLocationAsync(string query, int limit = 10)
    {
        _logger.LogInformation("[Mapbox] Search request - Query: '{Query}', Limit: {Limit}", query, limit);

        try
        {
            var encodedQuery = Uri.EscapeDataString(query);
            var url = $"{BaseUrl}/{encodedQuery}.json?access_token={_accessToken}&limit={limit}&types=place,locality,address";

            _logger.LogInformation("[Mapbox] Making API call: {Url}", url.Replace(_accessToken, "***"));

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Mapbox] API call failed with status {StatusCode}", response.StatusCode);
                return Result.Failure<SearchLocationResponse>(LocationErrors.ServiceUnavailable);
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<MapboxResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result?.Features == null || result.Features.Count == 0)
            {
                _logger.LogInformation("[Mapbox] No results found for query: '{Query}'", query);
                return Result.Failure<SearchLocationResponse>(LocationErrors.NoResults);
            }

            var locationResults = result.Features
                .Where(f => f.Geometry?.Coordinates != null && f.Geometry.Coordinates.Count >= 2)
                .Select(f => new LocationSearchResult
                {
                    DisplayName = f.PlaceName ?? string.Empty,
                    Latitude = f.Geometry!.Coordinates[1], // Mapbox uses [lon, lat]
                    Longitude = f.Geometry.Coordinates[0],
                    City = ExtractCity(f),
                    Country = ExtractCountry(f),
                    CountryCode = ExtractCountryCode(f)
                })
                .ToList();

            _logger.LogInformation("[Mapbox] Success! Found {Count} results for '{Query}'", locationResults.Count, query);

            return Result<SearchLocationResponse>.Success(new SearchLocationResponse
            {
                Results = locationResults
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Mapbox] Error searching location. Query: {Query}", query);
            return Result.Failure<SearchLocationResponse>(LocationErrors.ServiceUnavailable);
        }
    }

    public async Task<Result<ReverseGeocodeResponse>> ReverseGeocodeAsync(double latitude, double longitude)
    {
        _logger.LogInformation("[Mapbox] Reverse geocode request - Lat: {Lat}, Lon: {Lon}", latitude, longitude);

        try
        {
            var url = $"{BaseUrl}/{longitude.ToString(CultureInfo.InvariantCulture)},{latitude.ToString(CultureInfo.InvariantCulture)}.json?access_token={_accessToken}&types=place,locality";

            _logger.LogInformation("[Mapbox] Making reverse geocode API call");

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Mapbox] Reverse geocode failed with status {StatusCode}", response.StatusCode);
                return Result.Failure<ReverseGeocodeResponse>(LocationErrors.ServiceUnavailable);
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<MapboxResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result?.Features == null || result.Features.Count == 0)
            {
                return Result.Failure<ReverseGeocodeResponse>(LocationErrors.NoResults);
            }

            var feature = result.Features[0];
            var reverseResult = new ReverseGeocodeResponse
            {
                DisplayName = feature.PlaceName ?? string.Empty,
                City = ExtractCity(feature),
                Country = ExtractCountry(feature),
                CountryCode = ExtractCountryCode(feature)
            };

            _logger.LogInformation("[Mapbox] Reverse geocode success! Location: {DisplayName}", reverseResult.DisplayName);

            return Result<ReverseGeocodeResponse>.Success(reverseResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Mapbox] Error reverse geocoding: {Latitude}, {Longitude}", latitude, longitude);
            return Result.Failure<ReverseGeocodeResponse>(LocationErrors.ServiceUnavailable);
        }
    }

    private static string? ExtractCity(MapboxFeature feature)
    {
        if (feature.Context == null) return null;

        var place = feature.Context.FirstOrDefault(c => c.Id?.StartsWith("place.") == true);
        return place?.Text;
    }

    private static string? ExtractCountry(MapboxFeature feature)
    {
        if (feature.Context == null) return null;

        var country = feature.Context.FirstOrDefault(c => c.Id?.StartsWith("country.") == true);
        return country?.Text;
    }

    private static string? ExtractCountryCode(MapboxFeature feature)
    {
        if (feature.Context == null) return null;

        var country = feature.Context.FirstOrDefault(c => c.Id?.StartsWith("country.") == true);
        return country?.ShortCode?.ToUpperInvariant();
    }

    // Internal classes for JSON deserialization
    private class MapboxResponse
    {
        [JsonPropertyName("features")]
        public List<MapboxFeature> Features { get; set; } = new();
    }

    private class MapboxFeature
    {
        [JsonPropertyName("place_name")]
        public string? PlaceName { get; set; }

        [JsonPropertyName("geometry")]
        public MapboxGeometry? Geometry { get; set; }

        [JsonPropertyName("context")]
        public List<MapboxContext>? Context { get; set; }
    }

    private class MapboxGeometry
    {
        [JsonPropertyName("coordinates")]
        public List<double> Coordinates { get; set; } = new();
    }

    private class MapboxContext
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("short_code")]
        public string? ShortCode { get; set; }
    }
}
