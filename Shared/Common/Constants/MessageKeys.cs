namespace Shared.Common.Constants;

/// <summary>
/// Centralized message key constants used for client-side success message localization.
/// These keys must match the keys in localization/messages.json files.
/// </summary>
public static class MessageKeys
{
    // Auth related messages
    public const string EmailVerificationSent = "emailVerificationSent";
    public const string PasswordResetEmailSent = "passwordResetEmailSent";
    public const string PasswordResetSuccess = "passwordResetSuccess";
    public const string VerificationComplete = "verificationComplete";
    public const string RegistrationComplete = "registrationComplete";

    // Profile related messages
    public const string ProfileUpdated = "profileUpdated";
    public const string PasswordUpdated = "passwordUpdated";
    public const string EmailUpdated = "emailUpdated";
    public const string PreferencesUpdated = "preferencesUpdated";
    public const string AccountDeleted = "accountDeleted";
    public const string AccountDeletedWithDetails = "accountDeletedWithDetails";

    // Event related messages
    public const string EventCreatedSuccess = "eventCreatedSuccess";
    public const string EventUpdated = "eventUpdated";
    public const string EventJoinedSuccess = "eventJoinedSuccess";
    public const string EventLeftSuccess = "eventLeftSuccess";
    public const string EventDeleted = "eventDeleted";
    public const string ParticipantBlocked = "participantBlocked";
    public const string ParticipantUnblocked = "participantUnblocked";

    // Image related messages
    public const string ImageUploaded = "imageUploaded";
    public const string ImageGenerated = "imageGenerated";

    // General messages
    public const string FeatureComingSoon = "featureComingSoon";
}
