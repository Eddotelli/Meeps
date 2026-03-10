using System.Net.Http;
using System.Text.Json;
using Shared.Contracts.Locations;

namespace API.Infrastructure.Services;

public class LocationDetectionService : ILocationDetectionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocationDetectionService> _logger;

    // Free IP geolocation API - no key required, city-level accuracy
    private const string IpApiUrl = "http://ip-api.com/json/";

    // Default fallback for Swedish users
    private static readonly LocationSearchResult StockholmDefault = new()
    {
        City = "Stockholm",
        Country = "Sweden",
        CountryCode = "SE",
        Latitude = 59.3293,
        Longitude = 18.0686,
        DisplayName = "Stockholm, Sweden"
    };

    public LocationDetectionService(
        IHttpClientFactory httpClientFactory,
        ILogger<LocationDetectionService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(5); // Quick timeout
        _logger = logger;
    }

    public async Task<LocationSearchResult?> DetectLocationFromIP(string? ipAddress)
    {
        // If no IP provided or localhost, return Stockholm default
        if (string.IsNullOrEmpty(ipAddress) ||
            ipAddress == "::1" ||
            ipAddress.StartsWith("127.") ||
            ipAddress.StartsWith("192.168.") ||
            ipAddress.StartsWith("10."))
        {
            _logger.LogInformation("[LocationDetection] Local IP detected, using Stockholm default");
            return StockholmDefault;
        }

        try
        {
            _logger.LogInformation("[LocationDetection] Detecting location for IP: {IP}", ipAddress);

            var url = $"{IpApiUrl}{ipAddress}?fields=status,message,country,countryCode,city,lat,lon";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[LocationDetection] IP-API returned status: {Status}", response.StatusCode);
                return StockholmDefault;
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<IpApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null || result.Status != "success")
            {
                _logger.LogWarning("[LocationDetection] IP-API failed: {Message}", result?.Message ?? "Unknown error");
                return StockholmDefault;
            }

            // Check if Swedish IP
            if (result.CountryCode != "SE")
            {
                _logger.LogInformation("[LocationDetection] Non-Swedish IP ({Country}), using Stockholm default",
                    result.Country);
                return StockholmDefault;
            }

            var location = new LocationSearchResult
            {
                City = result.City ?? "Stockholm",
                Country = result.Country ?? "Sweden",
                CountryCode = result.CountryCode ?? "SE",
                Latitude = result.Lat ?? StockholmDefault.Latitude,
                Longitude = result.Lon ?? StockholmDefault.Longitude,
                DisplayName = $"{result.City ?? "Stockholm"}, {result.Country ?? "Sweden"}"
            };

            _logger.LogInformation("[LocationDetection] Detected location: {City}, {Country}",
                location.City, location.Country);

            return location;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LocationDetection] Error detecting location from IP");
            return StockholmDefault;
        }
    }

    private class IpApiResponse
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public string? Country { get; set; }
        public string? CountryCode { get; set; }
        public string? City { get; set; }
        public double? Lat { get; set; }
        public double? Lon { get; set; }
    }
}
