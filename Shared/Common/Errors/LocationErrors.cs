using Shared.Common.Results;

namespace Shared.Common.Errors;

public static class LocationErrors
{
    public static Error GeocodingFailed => new(
        "LOCATION.GEOCODING_FAILED",
        "Could not find that address");

    public static Error InvalidCoordinates => new(
        "LOCATION.INVALID_COORDINATES",
        "Invalid coordinates provided");

    public static Error ServiceUnavailable => new(
        "LOCATION.SERVICE_UNAVAILABLE",
        "Location service temporarily unavailable");

    public static Error NoResults => new(
        "LOCATION.NO_RESULTS",
        "No locations found for the search query");

    public static Error RateLimitExceeded => new(
        "LOCATION.RATE_LIMIT_EXCEEDED",
        "Too many requests. Please try again later");

    public static Error NotSet => new(
        "LOCATION.NOT_SET",
        "User must set a default location or provide coordinates");
}
