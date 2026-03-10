using Shared.Common.Results;

namespace Shared.Common.Errors;

public static class UserErrors
{
    public static readonly Error EmailAlreadyExists = new(
        "USER.EMAIL_EXISTS",
        "A user with this email already exists",
        409);

    public static readonly Error NotFound = new(
        "USER.NOT_FOUND",
        "User not found",
        404);

    public static readonly Error RegistrationFailed = new(
        "USER.REGISTRATION_FAILED",
        "User registration failed",
        400);

    public static readonly Error UpdateFailed = new(
        "USER.UPDATE_FAILED",
        "Failed to update user",
        400);

    public static readonly Error DisplayNameAlreadyExists = new(
        "USER.DISPLAY_NAME_EXISTS",
        "This display name is already taken",
        409);

    public static readonly Error PasswordSetFailed = new(
        "USER.PASSWORD_FAILED",
        "Failed to set password",
        400);

    public static readonly Error InvalidPassword = new(
        "USER.INVALID_PASSWORD",
        "Current password is incorrect",
        400);

    public static readonly Error PasswordChangeFailed = new(
        "USER.PASSWORD_CHANGE_FAILED",
        "Failed to change password",
        400);

    public static readonly Error EmailChangeFailed = new(
        "USER.EMAIL_CHANGE_FAILED",
        "Failed to change email",
        400);

    public static readonly Error LockedOut = new(
        "USER.LOCKED_OUT",
        "Account is locked due to too many failed login attempts",
        429);

    public static readonly Error ProfileAlreadyComplete = new(
        "USER.PROFILE_COMPLETE",
        "User profile is already complete",
        400);

    public static readonly Error ProfileNotComplete = new(
        "USER.PROFILE_INCOMPLETE",
        "User profile must be completed before login",
        400);

    public static readonly Error Unauthorized = new(
        "USER.UNAUTHORIZED",
        "You are not authorized to view this profile",
        403);
}
