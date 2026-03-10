using Shared.Common.Results;

namespace Shared.Common.Errors;

public static class EmailErrors
{
    public static readonly Error InvalidToken = new(
        "EMAIL.INVALID_TOKEN",
        "Verification token is invalid or has expired",
        400);

    public static readonly Error SendFailed = new(
        "EMAIL.SEND_FAILED",
        "Failed to send email",
        500);

    public static readonly Error AlreadyVerified = new(
        "EMAIL.ALREADY_VERIFIED",
        "Email address is already verified",
        400);
}
