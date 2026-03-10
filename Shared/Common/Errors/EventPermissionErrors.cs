using Shared.Common.Results;

namespace Shared.Common.Errors;

public static class EventPermissionErrors
{
    public static readonly Error NotParticipant = new(
        "EVENT.NOT_PARTICIPANT",
        "You are not a participant of this event",
        403);

    public static readonly Error NotCreator = new(
        "EVENT.NOT_CREATOR",
        "Only the event creator can perform this action",
        403);

    public static readonly Error NotAuthorized = new(
        "EVENT.NOT_AUTHORIZED",
        "You are not authorized to perform this action on this event",
        403);

    public static readonly Error AlreadyParticipant = new(
        "EVENT.ALREADY_PARTICIPANT",
        "User is already a participant of this event",
        409);

    public static readonly Error CannotRemoveCreator = new(
        "EVENT.CANNOT_REMOVE_CREATOR",
        "Cannot remove the event creator. Delete the event instead",
        400);

    public static readonly Error CannotChangeCreatorRole = new(
        "EVENT.CANNOT_CHANGE_CREATOR_ROLE",
        "Cannot change the role of the event creator",
        400);

    public static readonly Error MultipleCreatorsNotAllowed = new(
        "EVENT.MULTIPLE_CREATORS_NOT_ALLOWED",
        "An event can only have one creator",
        400);

    public static readonly Error InvalidRoleTransition = new(
        "EVENT.INVALID_ROLE_TRANSITION",
        "Invalid role transition",
        400);

    public static readonly Error ParticipantNotFound = new(
        "EVENT.PARTICIPANT_NOT_FOUND",
        "Participant not found in this event",
        404);

    public static readonly Error CannotModifyOwnStatus = new(
        "EVENT.CANNOT_MODIFY_OWN_STATUS",
        "Cannot modify your own participant status through this endpoint",
        400);

    public static readonly Error EventFull = new(
        "EVENT.EVENT_FULL",
        "Event has reached maximum attendance",
        400);

    public static readonly Error InvitationPending = new(
        "EVENT.INVITATION_PENDING",
        "Invitation is already pending",
        400);

    public static readonly Error MustBeCoOrganizerOrCreator = new(
        "EVENT.MUST_BE_CO_ORGANIZER_OR_CREATOR",
        "Only event creator or co-organizers can perform this action",
        403);

    public static readonly Error CannotManageParticipants = new(
        "EVENT.CANNOT_MANAGE_PARTICIPANTS",
        "You do not have permission to manage participants for this event",
        403);

    public static readonly Error ParticipantAlreadyBlocked = new(
        "EVENT.PARTICIPANT_ALREADY_BLOCKED",
        "Participant is already blocked from this event",
        400);

    public static readonly Error ParticipantNotBlocked = new(
        "EVENT.PARTICIPANT_NOT_BLOCKED",
        "Participant is not blocked from this event",
        400);

    public static readonly Error CannotBlockCreator = new(
        "EVENT.CANNOT_BLOCK_CREATOR",
        "Cannot block the event creator",
        400);

    public static readonly Error CannotBlockSelf = new(
        "EVENT.CANNOT_BLOCK_SELF",
        "Cannot block yourself from the event",
        400);
}
