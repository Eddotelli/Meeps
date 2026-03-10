using Shared.Contracts.Locations;

namespace API.Infrastructure.Services;

public interface ILocationDetectionService
{
    /// <summary>
    /// Detects approximate location (city-level) from an IP address.
    /// Falls back to Stockholm if detection fails or for non-Swedish IPs.
    /// </summary>
    Task<LocationSearchResult?> DetectLocationFromIP(string? ipAddress);
}
