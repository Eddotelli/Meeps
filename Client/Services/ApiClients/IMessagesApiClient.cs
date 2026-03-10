using Shared.Common.Results;
using Shared.Contracts.Messages;

namespace Client.Services.ApiClients;

public interface IMessagesApiClient
{
    Task<Result<SendMessageResponse>> SendMessageAsync(SendMessageRequest request);
    Task<Result<GetEventMessagesResponse>> GetEventMessagesAsync(string eventHash, int pageNumber = 1, int pageSize = 50);
}
