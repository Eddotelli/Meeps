using Shared.Common.Results;

namespace Shared.Common.Errors;

public static class CommonErrors
{
    public static class Validation
    {
        public static Error InvalidInput(string message) => new(
            "VALIDATION.INVALID_INPUT",
            message,
            400);

        public static readonly Error Required = new(
            "VALIDATION.REQUIRED",
            "A required field is missing",
            400);

        public static readonly Error ValidationError = new(
            "GENERAL.VALIDATION_ERROR",
            "Validation failed",
            400);
    }

    public static readonly Error ServerError = new(
        "GENERAL.SERVER_ERROR",
        "An unexpected error occurred",
        500);

    public static readonly Error Unauthorized = new(
        "GENERAL.UNAUTHORIZED",
        "Unauthorized access",
        401);

    public static readonly Error Forbidden = new(
        "GENERAL.FORBIDDEN",
        "Access denied",
        403);

    public static readonly Error NotFound = new(
        "GENERAL.NOT_FOUND",
        "The requested resource was not found",
        404);

    public static readonly Error InvalidOperation = new(
        "GENERAL.INVALID_OPERATION",
        "Invalid operation",
        400);

    public static class Client
    {
        public static readonly Error NullResponse = new(
            "CLIENT.NULL_RESPONSE",
            "No data received from server",
            0);

        public static readonly Error NetworkError = new(
            "CLIENT.NETWORK_ERROR",
            "Network error occurred",
            0);

        public static readonly Error UnknownError = new(
            "CLIENT.UNKNOWN_ERROR",
            "An unexpected error occurred",
            0);
    }
}
