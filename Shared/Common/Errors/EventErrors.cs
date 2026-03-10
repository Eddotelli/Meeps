using Shared.Common.Results;

namespace Shared.Common.Errors;

public static class EventErrors
{
    public static readonly Error NotFound = new(
        "EVENT.NOT_FOUND",
        "Event not found",
        404);

    public static readonly Error AlreadyExists = new(
        "EVENT.ALREADY_EXISTS",
        "Event already exists",
        409);

    public static readonly Error Unauthorized = new(
        "EVENT.UNAUTHORIZED",
        "You are not authorized to perform this action",
        403);

    public static readonly Error InvalidDateRange = new(
        "EVENT.INVALID_DATE_RANGE",
        "Event date must be in the future",
        400);

    public static readonly Error InvalidAttendance = new(
        "EVENT.INVALID_ATTENDANCE",
        "Max attendance must be greater than min attendance",
        400);

    public static readonly Error InvalidAgeRange = new(
        "EVENT.INVALID_AGE_RANGE",
        "Max age must be greater than min age",
        400);

    public static readonly Error CategoryNotFound = new(
        "EVENT.CATEGORY_NOT_FOUND",
        "Category not found",
        404);

    public static readonly Error EventNotActive = new(
        "EVENT.NOT_ACTIVE",
        "Event is not active",
        400);

    public static readonly Error EventHasPassed = new(
        "EVENT.HAS_PASSED",
        "Event has already passed",
        400);

    public static readonly Error AlreadyDeleted = new(
        "EVENT.ALREADY_DELETED",
        "Event has already been deleted",
        400);

    public static readonly Error CannotDeleteWithParticipants = new(
        "EVENT.CANNOT_DELETE_WITH_PARTICIPANTS",
        "Cannot delete event with active participants",
        400);

    public static readonly Error AlreadyParticipant = new(
        "EVENT.ALREADY_PARTICIPANT",
        "You are already a participant in this event",
        409);

    public static readonly Error EventFull = new(
        "EVENT.FULL",
        "Event has reached maximum capacity",
        400);

    public static readonly Error NotParticipant = new(
        "EVENT.NOT_PARTICIPANT",
        "You are not a participant in this event",
        409);

    public static readonly Error CannotLeaveOwnEvent = new(
        "EVENT.CANNOT_LEAVE_OWN_EVENT",
        "Event creator cannot leave their own event",
        400);

    public static readonly Error InvalidAgeRangeForParticipants = new(
        "EVENT.INVALID_AGE_RANGE",
        "Age range would exclude existing participants",
        400);

    public static readonly Error InvalidGenderRestrictionForParticipants = new(
        "EVENT.INVALID_GENDER_RESTRICTION",
        "Gender restriction would exclude existing participants",
        400);

    public static readonly Error InvalidGenderRestrictionForUser = new(
        "EVENT.INVALID_GENDER_RESTRICTION_FOR_USER",
        "You can only create events for your own gender group or for everyone",
        400);

    public static readonly Error InvalidMaxAttendanceForParticipants = new(
        "EVENT.INVALID_MAX_ATTENDANCE",
        "Cannot reduce max attendance below current participant count",
        400);

    public static readonly Error CannotRequireVerificationWithUnverified = new(
        "EVENT.CANNOT_REQUIRE_VERIFICATION",
        "Cannot require verification when unverified participants exist",
        400);
}
