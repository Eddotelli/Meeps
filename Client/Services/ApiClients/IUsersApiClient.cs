using Shared.Common.Results;
using Shared.Contracts.Users;

namespace Client.Services.ApiClients;

/// <summary>
/// Interface for users API client.
/// Handles all user-related API calls.
/// </summary>
public interface IUsersApiClient
{
    /// <summary>
    /// Gets the current authenticated user's profile.
    /// </summary>
    /// <returns>Result with user profile response</returns>
    Task<Result<GetUserProfileResponse>> GetUserProfileAsync();

    /// <summary>
    /// Gets profile edit constraints (which fields can be changed based on created events).
    /// </summary>
    /// <returns>Result with profile edit constraints</returns>
    Task<Result<GetProfileEditConstraintsResponse>> GetProfileEditConstraintsAsync();

    /// <summary>
    /// Updates the current authenticated user's profile.
    /// </summary>
    /// <returns>Result with updated user profile response</returns>
    Task<Result<GetUserProfileResponse>> UpdateProfileAsync(UpdateProfileRequest request);

    /// <summary>
    /// Verifies the current authenticated user (placeholder for BankID integration).
    /// </summary>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> VerifyUserAsync();

    /// <summary>
    /// Updates the current user's password.
    /// </summary>
    Task<Result> UpdatePasswordAsync(UpdatePasswordRequest request);

    /// <summary>
    /// Updates the current user's email.
    /// </summary>
    Task<Result> UpdateEmailAsync(UpdateEmailRequest request);

    /// <summary>
    /// Gets the current user's preferences (location and categories).
    /// </summary>
    Task<Result<GetUserPreferencesResponse>> GetUserPreferencesAsync();

    /// <summary>
    /// Updates the current user's preferences (location and categories combined).
    /// </summary>
    Task<Result<GetUserPreferencesResponse>> UpdatePreferencesAsync(UpdatePreferencesRequest request);

    /// <summary>
    /// Updates the current user's location preferences (city, coordinates, search radius).
    /// </summary>
    Task<Result<UpdateLocationResponse>> UpdateLocationAsync(UpdateLocationRequest request);

    /// <summary>
    /// Updates the current user's category preferences.
    /// </summary>
    Task<Result<UpdateCategoriesResponse>> UpdateCategoriesAsync(UpdateCategoriesRequest request);

    /// <summary>
    /// Deletes the current user's account.
    /// </summary>
    Task<Result<DeleteAccountResponse>> DeleteAccountAsync(DeleteAccountRequest request);
}
