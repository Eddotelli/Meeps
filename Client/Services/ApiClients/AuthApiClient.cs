using Shared.Contracts.Auth;
using Shared.Common.Results;

namespace Client.Services.ApiClients;

public class AuthApiClient : IAuthApiClient
{
    private readonly ApiClient _apiClient;

    public AuthApiClient(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<Result<LoginResponse>> LoginAsync(LoginRequest request)
        => _apiClient.PostAsync<LoginResponse>("/api/auth/login", request);

    public Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request)
        => _apiClient.PostAsync<RegisterResponse>("/api/auth/register", request);

    public Task<Result<VerifyEmailResponse>> VerifyEmailAsync(VerifyEmailRequest request)
        => _apiClient.PostAsync<VerifyEmailResponse>("/api/auth/verify-email", request);

    public Task<Result<CompleteRegistrationResponse>> CompleteRegistrationAsync(CompleteRegistrationRequest request)
        => _apiClient.PostAsync<CompleteRegistrationResponse>("/api/auth/complete-registration", request);

    public Task<Result<RefreshTokenResponse>> RefreshTokenAsync()
        => _apiClient.PostAsync<RefreshTokenResponse>("/api/auth/refresh-token", new { });

    public Task<Result> LogoutAsync()
        => _apiClient.PostAsync("/api/auth/logout", new { });

    public Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(ForgotPasswordRequest request)
        => _apiClient.PostAsync<ForgotPasswordResponse>("/api/auth/forgot-password", request);

    public Task<Result<ResetPasswordResponse>> ResetPasswordAsync(ResetPasswordRequest request)
        => _apiClient.PostAsync<ResetPasswordResponse>("/api/auth/reset-password", request);

    public Task<Result<ValidateResetTokenResponse>> ValidateResetTokenAsync(ValidateResetTokenRequest request)
        => _apiClient.PostAsync<ValidateResetTokenResponse>("/api/auth/validate-reset-token", request);
}
