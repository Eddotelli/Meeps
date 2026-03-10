using Shared.Common.Results;
using Shared.Contracts.Users;

namespace Client.Services.ApiClients;

public class UsersApiClient : IUsersApiClient
{
    private readonly ApiClient _apiClient;

    public UsersApiClient(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<Result<GetUserProfileResponse>> GetUserProfileAsync()
        => _apiClient.GetAsync<GetUserProfileResponse>("/api/users/me");

    public Task<Result<GetProfileEditConstraintsResponse>> GetProfileEditConstraintsAsync()
        => _apiClient.GetAsync<GetProfileEditConstraintsResponse>("/api/users/profile/edit-constraints");

    public Task<Result<GetUserProfileResponse>> UpdateProfileAsync(UpdateProfileRequest request)
        => _apiClient.PutAsync<GetUserProfileResponse>("/api/users/me", request);

    public Task<Result> VerifyUserAsync()
        => _apiClient.PostAsync("/api/users/verify", new { });

    public Task<Result> UpdatePasswordAsync(UpdatePasswordRequest request)
        => _apiClient.PostAsync("/api/users/password", request);

    public Task<Result> UpdateEmailAsync(UpdateEmailRequest request)
        => _apiClient.PostAsync("/api/users/email", request);

    public Task<Result<GetUserPreferencesResponse>> GetUserPreferencesAsync()
        => _apiClient.GetAsync<GetUserPreferencesResponse>("/api/users/preferences");

    public Task<Result<GetUserPreferencesResponse>> UpdatePreferencesAsync(UpdatePreferencesRequest request)
        => _apiClient.PutAsync<GetUserPreferencesResponse>("/api/users/preferences", request);

    public Task<Result<UpdateLocationResponse>> UpdateLocationAsync(UpdateLocationRequest request)
        => _apiClient.PutAsync<UpdateLocationResponse>("/api/users/location", request);

    public Task<Result<UpdateCategoriesResponse>> UpdateCategoriesAsync(UpdateCategoriesRequest request)
        => _apiClient.PutAsync<UpdateCategoriesResponse>("/api/users/categories", request);

    public async Task<Result<DeleteAccountResponse>> DeleteAccountAsync(DeleteAccountRequest request)
    {
        return await _apiClient.PostAsync<DeleteAccountResponse>(
            "/api/users/account/delete",
            request
        );
    }
}
