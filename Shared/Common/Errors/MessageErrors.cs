using Shared.Common.Results;

namespace Shared.Common.Errors;

public static class MessageErrors
{
    public static Error EventNotFound => new(
        "Message.EventNotFound",
        "The specified event does not exist"
    );

    public static Error NotParticipant => new(
        "Message.NotParticipant",
        "You must be a participant of the event to send messages"
    );

    public static Error EventInactive => new(
        "Message.EventInactive",
        "Cannot send messages to an inactive event"
    );

    public static Error MessageTooLong => new(
        "Message.MessageTooLong",
        "Message exceeds maximum length"
    );

    public static Error EmptyMessage => new(
        "Message.EmptyMessage",
        "Message cannot be empty"
    );

    public static Error UnauthorizedAccess => new(
        "Message.UnauthorizedAccess",
        "You are not authorized to view messages for this event"
    );

    public static Error InvalidPagination => new(
        "Message.InvalidPagination",
        "Invalid pagination parameters"
    );

    public static Error MessageBlocked => new(
        "Message.Blocked",
        "Your message was blocked due to inappropriate content"
    );
}

public static class ModerationErrors
{
    public static Error ServiceUnavailable => new(
        "Moderation.ServiceUnavailable",
        "Content moderation service is temporarily unavailable"
    );
}
