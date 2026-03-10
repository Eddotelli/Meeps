using System.Security.Claims;
using Shared.Common.Errors;
using Shared.Common.Results;
using API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Auth.Logout;

public class LogoutHandler
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LogoutHandler> _logger;

    public LogoutHandler(
        ApplicationDbContext context,
        ILogger<LogoutHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(ClaimsPrincipal user)
    {
        // Get user ID from claims
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Result.Failure(CommonErrors.Unauthorized);
        }

        // Revoke all active refresh tokens for this user
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} logged out. {TokenCount} tokens revoked.", userId, activeTokens.Count);

        return Result.Success();
    }
}
