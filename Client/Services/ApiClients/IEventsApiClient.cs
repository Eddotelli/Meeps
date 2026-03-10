using Shared.Contracts.Events;
using Shared.Common.Results;

namespace Client.Services.ApiClients;

/// <summary>
/// Interface for events API client.
/// Handles all event-related API calls.
/// </summary>
public interface IEventsApiClient
{
    /// <summary>
    /// Creates a new event.
    /// </summary>
    /// <param name="request">Event creation request</param>
    /// <returns>Result with created event information</returns>
    Task<Result<CreateEventResponse>> CreateEventAsync(CreateEventRequest request);

    /// <summary>
    /// Gets details for a specific event.
    /// </summary>
    /// <param name="eventHash">Event hash ID</param>
    /// <returns>Result with event details</returns>
    Task<Result<GetEventDetailsResponse>> GetEventDetailsAsync(string eventHash);

    /// <summary>
    /// Gets all events created by the current user.
    /// </summary>
    /// <returns>Result with list of user's created events</returns>
    Task<Result<List<GetEventDetailsResponse>>> GetMyEventsAsync();

    /// <summary>
    /// Gets all events the current user is participating in.
    /// </summary>
    /// <returns>Result with list of participating events</returns>
    Task<Result<List<GetEventDetailsResponse>>> GetMyParticipatingEventsAsync();

    /// <summary>
    /// Gets all archived (past) events where user was creator or participant.
    /// </summary>
    /// <returns>Result with list of archived events</returns>
    Task<Result<List<GetEventDetailsResponse>>> GetMyArchivedEventsAsync();

    /// <summary>
    /// Gets eligible events for the current user based on their profile (gender, age) and location preferences.
    /// </summary>
    /// <param name="request">Request with optional location and filter parameters</param>
    /// <returns>Result with list of eligible events for the user</returns>
    Task<Result<GetEligibleEventsResponse>> GetEligibleEventsAsync(GetEligibleEventsRequest request);

    /// <summary>
    /// Gets edit constraints for a specific event based on current participants.
    /// </summary>
    /// <param name="eventId">Event ID</param>
    /// <returns>Result with edit constraints (age limits, gender restrictions, etc.)</returns>
    Task<Result<GetEventEditConstraintsResponse>> GetEventEditConstraintsAsync(int eventId);

    /// <summary>
    /// Updates an existing event.
    /// </summary>
    /// <param name="request">Event update request</param>
    /// <returns>Result with updated event information</returns>
    Task<Result<UpdateEventResponse>> UpdateEventAsync(UpdateEventRequest request);

    /// <summary>
    /// Joins an event as a participant.
    /// </summary>
    /// <param name="eventHash">Event hash ID to join</param>
    /// <returns>Result with join confirmation</returns>
    Task<Result<JoinEventResponse>> JoinEventAsync(string eventHash);

    /// <summary>
    /// Leaves an event as a participant.
    /// </summary>
    /// <param name="eventHash">Event hash ID to leave</param>
    /// <returns>Result with leave confirmation</returns>
    Task<Result<LeaveEventResponse>> LeaveEventAsync(string eventHash);

    /// <summary>
    /// Deletes an event.
    /// </summary>
    /// <param name="eventId">Event ID to delete</param>
    /// <returns>Result with deletion confirmation</returns>
    Task<Result<DeleteEventResponse>> DeleteEventAsync(string eventHash);

    /// <summary>
    /// Blocks a participant from an event.
    /// </summary>
    /// <param name="request">Block participant request</param>
    /// <returns>Result with block confirmation</returns>
    Task<Result<BlockParticipantResponse>> BlockParticipantAsync(BlockParticipantRequest request);

    /// <summary>
    /// Unblocks a participant from an event.
    /// </summary>
    /// <param name="request">Unblock participant request</param>
    /// <returns>Result with unblock confirmation</returns>
    Task<Result<UnblockParticipantResponse>> UnblockParticipantAsync(UnblockParticipantRequest request);
}
