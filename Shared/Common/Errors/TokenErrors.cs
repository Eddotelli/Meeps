using Shared.Common.Results;

namespace Shared.Common.Errors;

public static class TokenErrors
{
    public static readonly Error Invalid = new(
        "TOKEN.INVALID",
        "Token is invalid",
        401);

    public static readonly Error Expired = new(
        "TOKEN.EXPIRED",
        "Token has expired",
        401);

    public static readonly Error NotFound = new(
        "TOKEN.NOT_FOUND",
        "Token not found",
        404);

    public static readonly Error Revoked = new(
        "TOKEN.REVOKED",
        "Token has been revoked",
        401);
}
