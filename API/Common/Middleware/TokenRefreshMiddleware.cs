using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using API.Common.Extensions;
using API.Features.Auth.RefreshToken;

namespace API.Common.Middleware;

/// <summary>
/// Middleware that automatically refreshes access tokens before they expire or when expired.
/// BFF Pattern: Backend-driven token refresh for seamless user experience.
/// Supports "Keep me logged in" - refreshes expired tokens if RefreshToken is still valid.
/// </summary>
public class TokenRefreshMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenRefreshMiddleware> _logger;
    private const int RefreshThresholdMinutes = 5;

    // Race condition protection: One refresh per user at a time
    private static readonly ConcurrentDictionary<string, (SemaphoreSlim Lock, DateTime LastUsed)> _refreshLocks = new();

    // Token cache: Prevents race conditions when multiple requests try to refresh simultaneously
    // Key: userId, Value: (AccessToken, RefreshToken, ExpiresAt)
    private static readonly ConcurrentDictionary<string, (string AccessToken, string RefreshToken, DateTime ExpiresAt)> _tokenCache = new();

    private static DateTime _lastCleanup = DateTime.UtcNow;

    public TokenRefreshMiddleware(
        RequestDelegate next,
        ILogger<TokenRefreshMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IConfiguration configuration,
        RefreshTokenHandler refreshTokenHandler)
    {
        // Periodic cleanup of old locks and cached tokens (every 10 minutes)
        if (DateTime.UtcNow - _lastCleanup > TimeSpan.FromMinutes(10))
        {
            CleanupOldLocks();
            CleanupExpiredTokenCache();
            _lastCleanup = DateTime.UtcNow;
        }

        try
        {
            // Check if we have tokens in cookies (might be expired, but that's OK - we can refresh!)
            var accessToken = context.Request.Cookies["AccessToken"];
            var refreshToken = context.Request.Cookies["RefreshToken"];

            // Only attempt refresh if we have both cookies
            if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
            {
                var (shouldRefresh, userId) = ShouldRefreshToken(accessToken);

                if (shouldRefresh && !string.IsNullOrEmpty(userId))
                {
                    // Check cache first: If another request just refreshed, use cached tokens
                    if (_tokenCache.TryGetValue(userId, out var cachedTokens))
                    {
                        // Use cached token if it's still valid and fresh enough
                        if (cachedTokens.ExpiresAt > DateTime.UtcNow.AddMinutes(1))
                        {
                            // Cached token is still fresh - use it instead of refreshing again
                            _logger.LogDebug(
                                "Using cached tokens for user {UserId}. Expires at {ExpiresAt}",
                                userId, cachedTokens.ExpiresAt);

                            // Update cookies with cached tokens (browser might not have them yet)
                            context.SetAuthCookies(
                                cachedTokens.AccessToken,
                                cachedTokens.RefreshToken,
                                configuration,
                                cachedTokens.ExpiresAt);

                            // Set context.User from cached token
                            var tokenHandler = new JwtSecurityTokenHandler();
                            var jwtToken = tokenHandler.ReadJwtToken(cachedTokens.AccessToken);
                            var claims = jwtToken.Claims;
                            var identity = new ClaimsIdentity(claims, "Bearer");
                            context.User = new ClaimsPrincipal(identity);

                            // Skip refresh - continue to next middleware
                            await _next(context);
                            return;
                        }
                        else
                        {
                            // Cached token expired - remove from cache
                            _tokenCache.TryRemove(userId, out _);
                        }
                    }

                    // Race condition protection: Only one refresh per user at a time
                    var userLock = _refreshLocks.GetOrAdd(userId, _ => (new SemaphoreSlim(1, 1), DateTime.UtcNow));

                    // Update last used timestamp
                    _refreshLocks[userId] = (userLock.Lock, DateTime.UtcNow);

                    // Non-blocking check: If another request is refreshing, skip this one
                    if (await userLock.Lock.WaitAsync(TimeSpan.Zero))
                    {
                        try
                        {
                            // Double-check cache again after acquiring lock
                            if (_tokenCache.TryGetValue(userId, out cachedTokens) &&
                                cachedTokens.ExpiresAt > DateTime.UtcNow.AddMinutes(1))
                            {
                                _logger.LogDebug(
                                    "Another request refreshed tokens for user {UserId}, using cached. Expires at {ExpiresAt}",
                                    userId, cachedTokens.ExpiresAt);

                                context.SetAuthCookies(
                                    cachedTokens.AccessToken,
                                    cachedTokens.RefreshToken,
                                    configuration,
                                    cachedTokens.ExpiresAt);

                                var tokenHandler = new JwtSecurityTokenHandler();
                                var jwtToken = tokenHandler.ReadJwtToken(cachedTokens.AccessToken);
                                var claims = jwtToken.Claims;
                                var identity = new ClaimsIdentity(claims, "Bearer");
                                context.User = new ClaimsPrincipal(identity);
                            }
                            else
                            {
                                // Double-check if refresh is still needed
                                var currentToken = context.Request.Cookies["AccessToken"];
                                if (!string.IsNullOrEmpty(currentToken))
                                {
                                    var (stillNeedsRefresh, _) = ShouldRefreshToken(currentToken);
                                    if (stillNeedsRefresh)
                                    {
                                        await RefreshTokensAsync(context, configuration, refreshTokenHandler, userId);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            userLock.Lock.Release();
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Token refresh already in progress for user {UserId}, waiting for cache", userId);

                        // Wait briefly for the other request to finish and cache the tokens
                        await Task.Delay(50);

                        // Try cache again
                        if (_tokenCache.TryGetValue(userId, out cachedTokens) &&
                            cachedTokens.ExpiresAt > DateTime.UtcNow.AddMinutes(1))
                        {
                            _logger.LogDebug(
                                "Using newly cached tokens for user {UserId}. Expires at {ExpiresAt}",
                                userId, cachedTokens.ExpiresAt);

                            context.SetAuthCookies(
                                cachedTokens.AccessToken,
                                cachedTokens.RefreshToken,
                                configuration,
                                cachedTokens.ExpiresAt);

                            var tokenHandler = new JwtSecurityTokenHandler();
                            var jwtToken = tokenHandler.ReadJwtToken(cachedTokens.AccessToken);
                            var claims = jwtToken.Claims;
                            var identity = new ClaimsIdentity(claims, "Bearer");
                            context.User = new ClaimsPrincipal(identity);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // CRITICAL: Never block request on error - log and continue
            _logger.LogError(ex, "Error in TokenRefreshMiddleware. Request continues.");
        }

        // Always continue to next middleware
        await _next(context);
    }

    private (bool ShouldRefresh, string? UserId) ShouldRefreshToken(string accessToken)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            if (!tokenHandler.CanReadToken(accessToken))
                return (false, null);

            var jwtToken = tokenHandler.ReadJwtToken(accessToken);

            // Extract userId from token (needed for locking mechanism)
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return (false, null);

            // Check token age: Skip refresh if token is very new
            // This prevents race conditions where multiple requests try to refresh the same old token
            // Use 50% of access token lifetime as threshold (e.g., 30s for 1min token, 7.5min for 15min token)
            var tokenAge = DateTime.UtcNow - jwtToken.IssuedAt;
            var tokenLifetime = jwtToken.ValidTo - jwtToken.IssuedAt;
            var freshnessThreshold = tokenLifetime.TotalSeconds * 0.5;

            if (tokenAge < TimeSpan.FromSeconds(freshnessThreshold))
            {
                _logger.LogDebug(
                    "Token for user {UserId} is too fresh (age: {Age}s, threshold: {Threshold}s). Skipping refresh.",
                    userId, (int)tokenAge.TotalSeconds, (int)freshnessThreshold);
                return (false, userId); // Token is too fresh - likely just refreshed by another request
            }

            var expiryTime = jwtToken.ValidTo;
            var timeUntilExpiry = expiryTime - DateTime.UtcNow;

            // Refresh if:
            // 1. Token expires within threshold (proactive refresh)
            // 2. Token is already expired (keep-me-logged-in: user was inactive but RefreshToken still valid)
            var shouldRefresh = timeUntilExpiry <= TimeSpan.FromMinutes(RefreshThresholdMinutes);

            if (shouldRefresh && timeUntilExpiry.TotalMinutes < 0)
            {
                _logger.LogInformation(
                    "Access token expired for user {UserId}. Attempting automatic refresh (Keep-me-logged-in feature).",
                    userId);
            }

            return (shouldRefresh, userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JWT token for refresh check");
            return (false, null);
        }
    }

    private async Task RefreshTokensAsync(
        HttpContext context,
        IConfiguration configuration,
        RefreshTokenHandler refreshTokenHandler,
        string userId)
    {
        var refreshToken = context.GetRefreshTokenFromCookie();

        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogDebug("No refresh token found in cookies");
            return;
        }

        var result = await refreshTokenHandler.HandleAsync(refreshToken);

        if (result.IsSuccess)
        {
            var accessTokenExpirationMinutes = int.Parse(configuration["Jwt:AccessTokenExpirationMinutes"]!);
            var tokenExpiresAt = DateTime.UtcNow.AddMinutes(accessTokenExpirationMinutes);

            // Store in cache FIRST (other requests might be waiting for this)
            _tokenCache[userId] = (
                result.Value.AccessToken,
                result.Value.NewRefreshToken,
                tokenExpiresAt
            );

            // Set new cookies with fresh tokens and actual expiry (respects sliding expiration)
            context.SetAuthCookies(
                result.Value.AccessToken,
                result.Value.NewRefreshToken,
                configuration,
                result.Value.RefreshTokenExpiry);

            // CRITICAL: Set context.User so UseAuthorization sees authenticated user
            // Without this, the current request would get 401 even though refresh succeeded
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(result.Value.AccessToken);
            var claims = jwtToken.Claims;
            var identity = new ClaimsIdentity(claims, "Bearer");
            context.User = new ClaimsPrincipal(identity);

            _logger.LogInformation(
                "Access token automatically refreshed for user {UserId} via middleware",
                userId);
        }
        else
        {
            _logger.LogWarning(
                "Automatic token refresh failed: {ErrorCode}. Clearing auth cookies.",
                result.Error?.Code);

            // Remove from cache on failure
            _tokenCache.TryRemove(userId, out _);

            // Clear auth cookies so user knows they are logged out
            context.ClearAuthCookies();
        }
    }

    private static void CleanupOldLocks()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        var keysToRemove = _refreshLocks
            .Where(kvp => kvp.Value.LastUsed < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            if (_refreshLocks.TryRemove(key, out var removed))
            {
                removed.Lock.Dispose();
            }
        }
    }

    private static void CleanupExpiredTokenCache()
    {
        var now = DateTime.UtcNow;
        var keysToRemove = _tokenCache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _tokenCache.TryRemove(key, out _);
        }
    }
}
