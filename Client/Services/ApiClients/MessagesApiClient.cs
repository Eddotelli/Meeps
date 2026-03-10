using Shared.Common.Results;
using Shared.Contracts.Messages;

namespace Client.Services.ApiClients;

public class MessagesApiClient : IMessagesApiClient
{
    private readonly ApiClient _apiClient;

    public MessagesApiClient(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<Result<SendMessageResponse>> SendMessageAsync(SendMessageRequest request)
    {
        return await _apiClient.PostAsync<SendMessageResponse>("/api/messages", request);
    }

    public async Task<Result<GetEventMessagesResponse>> GetEventMessagesAsync(
        string eventHash,
        int pageNumber = 1,
        int pageSize = 50)
    {
        return await _apiClient.GetAsync<GetEventMessagesResponse>(
            $"/api/messages/{eventHash}?pageNumber={pageNumber}&pageSize={pageSize}");
    }
}
