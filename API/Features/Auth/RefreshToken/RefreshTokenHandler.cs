using Shared.Common.Errors;
using Shared.Common.Results;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Auth;

namespace API.Features.Auth.RefreshToken;

public class RefreshTokenHandler
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RefreshTokenHandler> _logger;

    public RefreshTokenHandler(
        UserManager<User> userManager,
        ApplicationDbContext context,
        ITokenService tokenService,
        IConfiguration configuration,
        ILogger<RefreshTokenHandler> logger)
    {
        _userManager = userManager;
        _context = context;
        _tokenService = tokenService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<(RefreshTokenResponse Response, string AccessToken, string NewRefreshToken, DateTime RefreshTokenExpiry)>> HandleAsync(string refreshTokenFromCookie)
    {
        // Find refresh token in database
        var refreshToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenFromCookie);

        if (refreshToken == null)
        {
            return Result.Failure<(RefreshTokenResponse, string, string, DateTime)>(TokenErrors.NotFound);
        }

        // Validate refresh token
        if (refreshToken.IsRevoked)
        {
            // ⚠️ SECURITY INCIDENT: Revoked token reuse detected
            // This indicates potential token theft - revoke ALL tokens for this user
            _logger.LogWarning(
                "Security Alert: Revoked refresh token reuse detected for user {UserId}. " +
                "This may indicate token theft. Revoking all tokens for this user.",
                refreshToken.UserId);

            // Revoke ALL refresh tokens for this user as a security measure
            var allUserTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == refreshToken.UserId && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in allUserTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogWarning(
                "Security response: {TokenCount} active tokens revoked for user {UserId} due to token reuse detection.",
                allUserTokens.Count,
                refreshToken.UserId);

            // TODO: Send security alert email to user
            // await _emailService.SendSecurityAlertAsync(user.Email, "Suspicious activity detected");

            return Result.Failure<(RefreshTokenResponse, string, string, DateTime)>(TokenErrors.Revoked);
        }

        if (refreshToken.IsExpired)
        {
            return Result.Failure<(RefreshTokenResponse, string, string, DateTime)>(TokenErrors.Expired);
        }

        // Get user
        var user = refreshToken.User;

        // Generate new tokens
        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = _tokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        // Revoke old refresh token and create new one
        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.ReplacedByToken = newRefreshToken;

        // Implement sliding expiration logic
        var thresholdDays = int.Parse(_configuration["Jwt:SlidingExpirationThresholdDays"]!);
        var threshold = DateTime.UtcNow.AddDays(thresholdDays);

        // Calculate new expiry based on RememberMe flag
        var refreshTokenExpirationDays = refreshToken.RememberMe
            ? int.Parse(_configuration["Jwt:RefreshTokenExpirationDaysRememberMe"]!)
            : int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"]!);

        // Calculate absolute maximum expiry from original creation
        // This prevents tokens from being extended indefinitely through sliding expiration
        var absoluteMaxDays = int.Parse(_configuration["Jwt:RefreshTokenAbsoluteMaxDays"]!);
        var absoluteMaxExpiry = refreshToken.CreatedAt.AddDays(absoluteMaxDays);

        // Only extend if token expires within threshold (sliding window)
        var newExpiry = refreshToken.ExpiresAt < threshold
            ? DateTime.UtcNow.AddDays(refreshTokenExpirationDays)  // Extend
            : refreshToken.ExpiresAt;  // Keep same expiry

        // Ensure we never exceed absolute maximum expiry
        if (newExpiry > absoluteMaxExpiry)
        {
            newExpiry = absoluteMaxExpiry;
            _logger.LogInformation(
                "Token expiry capped at absolute maximum for user {UserId}. Original expiry: {OriginalExpiry}, Capped at: {CappedExpiry}",
                user.Id, newExpiry.AddDays(refreshTokenExpirationDays), absoluteMaxExpiry);
        }

        var wasExtended = refreshToken.ExpiresAt < threshold;

        var newRefreshTokenEntity = new Models.RefreshToken
        {
            Token = newRefreshToken,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = newExpiry,
            IsRevoked = false,
            RememberMe = refreshToken.RememberMe  // Preserve flag
        };

        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync();

        var expiryInfo = wasExtended
            ? $"extended to {newExpiry:yyyy-MM-dd HH:mm:ss} UTC"
            : $"kept at {newExpiry:yyyy-MM-dd HH:mm:ss} UTC";

        _logger.LogInformation(
            "Refresh token rotated for user {UserId}. Sliding expiration: {Extended}, expiry {ExpiryInfo} (RememberMe: {RememberMe})",
            user.Id, wasExtended, expiryInfo, refreshToken.RememberMe);

        var response = new RefreshTokenResponse("Tokens refreshed successfully");

        return Result.Success((response, newAccessToken, newRefreshToken, newRefreshTokenEntity.ExpiresAt));
    }
}
