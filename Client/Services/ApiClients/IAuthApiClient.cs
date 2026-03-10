using Shared.Contracts.Auth;
using Shared.Common.Results;

namespace Client.Services.ApiClients;

/// <summary>
/// Interface for authentication API client.
/// Handles all authentication-related API calls.
/// </summary>
public interface IAuthApiClient
{
    /// <summary>
    /// Logs in a user with email and password.
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>Result with login response including access token</returns>
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request);

    /// <summary>
    /// Registers a new user with email.
    /// Sends verification email.
    /// </summary>
    /// <param name="request">Registration request with email</param>
    /// <returns>Result with registration response</returns>
    Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request);

    /// <summary>
    /// Verifies a user's email address with verification token.
    /// </summary>
    /// <param name="request">Verification request with token</param>
    /// <returns>Result with verification response</returns>
    Task<Result<VerifyEmailResponse>> VerifyEmailAsync(VerifyEmailRequest request);

    /// <summary>
    /// Completes user registration by setting password and profile information.
    /// </summary>
    /// <param name="request">Complete registration request</param>
    /// <returns>Result with completion response</returns>
    Task<Result<CompleteRegistrationResponse>> CompleteRegistrationAsync(CompleteRegistrationRequest request);

    /// <summary>
    /// Refreshes the access token using the refresh token cookie.
    /// </summary>
    /// <returns>Result with new access token</returns>
    Task<Result<RefreshTokenResponse>> RefreshTokenAsync();

    /// <summary>
    /// Logs out the current user and revokes the refresh token.
    /// </summary>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> LogoutAsync();

    /// <summary>
    /// Sends a password reset link to the user's email address.
    /// </summary>
    /// <param name="request">Request with user's email</param>
    /// <returns>Result with confirmation message</returns>
    Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(ForgotPasswordRequest request);

    /// <summary>
    /// Resets user's password using a valid reset token.
    /// </summary>
    /// <param name="request">Request with token and new password</param>
    /// <returns>Result with confirmation message</returns>
    Task<Result<ResetPasswordResponse>> ResetPasswordAsync(ResetPasswordRequest request);

    /// <summary>
    /// Validates a password reset token to check if it's valid and not expired.
    /// </summary>
    /// <param name="request">Request with reset token</param>
    /// <returns>Result with validation status and error code if invalid</returns>
    Task<Result<ValidateResetTokenResponse>> ValidateResetTokenAsync(ValidateResetTokenRequest request);
}
