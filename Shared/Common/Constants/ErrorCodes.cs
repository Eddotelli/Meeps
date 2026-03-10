namespace Shared.Common.Constants;

/// <summary>
/// Centralized error code constants used for client-side error message localization.
/// These codes must match the keys in localization/errors.json files.
/// </summary>
public static class ErrorCodes
{
    // General errors
    public const string ServerError = "GENERAL.SERVER_ERROR";
    public const string Unauthorized = "GENERAL.UNAUTHORIZED";
    public const string Forbidden = "GENERAL.FORBIDDEN";
    public const string NotFound = "GENERAL.NOT_FOUND";
    public const string ValidationError = "GENERAL.VALIDATION_ERROR";

    // Email errors
    public const string EmailInvalidToken = "EMAIL.INVALID_TOKEN";
    public const string EmailInvalidLink = "EMAIL.INVALID_LINK";
    public const string EmailSendFailed = "EMAIL.SEND_FAILED";
    public const string EmailAlreadyVerified = "EMAIL.ALREADY_VERIFIED";

    // User/Auth errors
    public const string UserInvalidCredentials = "USER.INVALID_CREDENTIALS";
    public const string UserEmailNotVerified = "USER.EMAIL_NOT_VERIFIED";
    public const string UserEmailNotFound = "USER.EMAIL_NOT_FOUND";
    public const string UserPasswordResetTokenInvalid = "USER.PASSWORD_RESET_TOKEN_INVALID";
    public const string UserPasswordResetTokenExpired = "USER.PASSWORD_RESET_TOKEN_EXPIRED";
    public const string UserDisplayNameExists = "USER.DISPLAY_NAME_EXISTS";
    public const string UserIncorrectPassword = "USER.INCORRECT_PASSWORD";
    public const string UserDeleteAccountFailed = "USER.DELETE_ACCOUNT_FAILED";
    public const string UserGenderConflictWithEvents = "USER.GENDER_CONFLICT_WITH_EVENTS";
    public const string UserAgeConflictWithEvents = "USER.AGE_CONFLICT_WITH_EVENTS";

    // Verification errors
    public const string VerificationFailed = "VERIFICATION.FAILED";

    // Event errors
    public const string EventNotFound = "EVENT.NOT_FOUND";
    public const string EventNavigationFailed = "EVENT.NAVIGATION_FAILED";
    public const string EventCannotDelete = "EVENT.CANNOT_DELETE";
    public const string EventAlreadyDeleted = "EVENT.ALREADY_DELETED";

    // Location errors
    public const string LocationLoadFailed = "LOCATION.LOAD_FAILED";
    public const string LocationSaveFailed = "LOCATION.SAVE_FAILED";
    public const string LocationUpdateRadiusFailed = "LOCATION.UPDATE_RADIUS_FAILED";
    public const string LocationEventsLoadFailed = "LOCATION.EVENTS_LOAD_FAILED";

    // Image errors
    public const string ImageInvalidFormat = "IMAGE.INVALID_FORMAT";
    public const string ImageFileTooLarge = "IMAGE.FILE_TOO_LARGE";
    public const string ImageGenerationFailed = "IMAGE.GENERATION_FAILED";
    public const string ImageUploadFailed = "IMAGE.UPLOAD_FAILED";
    public const string ImageInappropriateContent = "IMAGE.INAPPROPRIATE_CONTENT";
    public const string ImageInvalidContext = "IMAGE.INVALID_CONTEXT";
    public const string ImageNoFile = "IMAGE.NO_FILE";

    // Message errors
    public const string MessageBlocked = "MESSAGE.BLOCKED";
    public const string MessageEventNotFound = "MESSAGE.EVENT_NOT_FOUND";
    public const string MessageUnauthorized = "MESSAGE.UNAUTHORIZED";

    // Moderation errors
    public const string ModerationServiceUnavailable = "MODERATION.SERVICE_UNAVAILABLE";
}
