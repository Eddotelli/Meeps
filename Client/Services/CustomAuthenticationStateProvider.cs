using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;
using Shared.Contracts.Auth;

namespace Client.Services;

/// <summary>
/// Custom authentication state provider for Blazor WebAssembly.
/// Determines authentication state by checking for AccessToken cookie.
/// </summary>
public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;

    public CustomAuthenticationStateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // Check if AccessToken cookie exists by making a simple call to the API
            // The cookie is automatically sent with the request
            var response = await _httpClient.GetAsync("/api/auth/check");

            if (response.IsSuccessStatusCode)
            {
                var userInfo = await response.Content.ReadFromJsonAsync<CheckAuthResponse>();

                if (userInfo != null && !string.IsNullOrEmpty(userInfo.Email))
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Email, userInfo.Email),
                        new Claim(ClaimTypes.Name, userInfo.DisplayName ?? userInfo.Email),
                        new Claim(ClaimTypes.NameIdentifier, userInfo.UserId.ToString())
                    };

                    var identity = new ClaimsIdentity(claims, "cookie");
                    var user = new ClaimsPrincipal(identity);

                    return new AuthenticationState(user);
                }
            }
        }
        catch
        {
            // If any error occurs, user is not authenticated
        }

        // Return anonymous user
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    /// <summary>
    /// Notify that authentication state has changed (e.g., after login/logout).
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
