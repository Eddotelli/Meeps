using Shared.Contracts.Events;
using Shared.Common.Results;

namespace Client.Services.ApiClients;

public class EventsApiClient : IEventsApiClient
{
    private readonly ApiClient _apiClient;

    public EventsApiClient(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<Result<CreateEventResponse>> CreateEventAsync(CreateEventRequest request)
        => _apiClient.PostAsync<CreateEventResponse>("/api/events", request);

    public Task<Result<GetEventDetailsResponse>> GetEventDetailsAsync(string eventHash)
        => _apiClient.GetAsync<GetEventDetailsResponse>($"/api/events/{eventHash}");

    public Task<Result<List<GetEventDetailsResponse>>> GetMyEventsAsync()
        => _apiClient.GetAsync<List<GetEventDetailsResponse>>("/api/events/my-events");

    public Task<Result<List<GetEventDetailsResponse>>> GetMyParticipatingEventsAsync()
        => _apiClient.GetAsync<List<GetEventDetailsResponse>>("/api/events/my-participating");

    public Task<Result<List<GetEventDetailsResponse>>> GetMyArchivedEventsAsync()
        => _apiClient.GetAsync<List<GetEventDetailsResponse>>("/api/events/my-archived");

    public Task<Result<GetEligibleEventsResponse>> GetEligibleEventsAsync(GetEligibleEventsRequest request)
    {
        var queryParams = new List<string>
        {
            $"pageNumber={request.PageNumber}",
            $"pageSize={request.PageSize}",
            $"sortBy={request.SortBy}"
        };

        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            queryParams.Add($"latitude={request.Latitude.Value}");
            queryParams.Add($"longitude={request.Longitude.Value}");
        }

        if (request.RadiusKm.HasValue)
        {
            queryParams.Add($"radiusKm={request.RadiusKm.Value}");
        }

        if (request.CategoryId.HasValue)
        {
            queryParams.Add($"categoryId={request.CategoryId.Value}");
        }

        if (request.StartDate.HasValue)
        {
            queryParams.Add($"startDate={request.StartDate.Value:O}");
        }

        var queryString = string.Join("&", queryParams);
        return _apiClient.GetAsync<GetEligibleEventsResponse>($"/api/events/eligible?{queryString}");
    }

    public Task<Result<GetEventEditConstraintsResponse>> GetEventEditConstraintsAsync(int eventId)
        => _apiClient.GetAsync<GetEventEditConstraintsResponse>($"/api/events/{eventId}/edit-constraints");

    public Task<Result<UpdateEventResponse>> UpdateEventAsync(UpdateEventRequest request)
        => _apiClient.PutAsync<UpdateEventResponse>($"/api/events/{request.EventHash}", request);

    public Task<Result<JoinEventResponse>> JoinEventAsync(string eventHash)
        => _apiClient.PostAsync<JoinEventResponse>($"/api/events/{eventHash}/join", new { });

    public Task<Result<LeaveEventResponse>> LeaveEventAsync(string eventHash)
        => _apiClient.PostAsync<LeaveEventResponse>($"/api/events/{eventHash}/leave", new { });

    public Task<Result<DeleteEventResponse>> DeleteEventAsync(string eventHash)
        => _apiClient.DeleteAsync<DeleteEventResponse>($"/api/events/{eventHash}");

    public Task<Result<BlockParticipantResponse>> BlockParticipantAsync(BlockParticipantRequest request)
        => _apiClient.PostAsync<BlockParticipantResponse>(
            $"/api/events/{request.EventId}/participants/{request.UserId}/block",
            request);

    public Task<Result<UnblockParticipantResponse>> UnblockParticipantAsync(UnblockParticipantRequest request)
        => _apiClient.DeleteAsync<UnblockParticipantResponse>(
            $"/api/events/{request.EventId}/participants/{request.UserId}/block");
}
