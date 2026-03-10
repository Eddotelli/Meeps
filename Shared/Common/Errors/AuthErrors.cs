using Shared.Common.Results;

namespace Shared.Common.Errors;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = new(
        "USER.INVALID_CREDENTIALS",
        "Invalid credentials",
        401);

    public static readonly Error EmailNotVerified = new(
        "USER.EMAIL_NOT_VERIFIED",
        "Email address is not verified",
        401);

    public static readonly Error EmailNotFound = new(
        "USER.EMAIL_NOT_FOUND",
        "Email address not found",
        404);

    public static readonly Error PasswordResetTokenInvalid = new(
        "USER.PASSWORD_RESET_TOKEN_INVALID",
        "Invalid password reset token",
        400);

    public static readonly Error PasswordResetTokenExpired = new(
        "USER.PASSWORD_RESET_TOKEN_EXPIRED",
        "Password reset token has expired",
        400);
}
