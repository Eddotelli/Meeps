using API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace API.Infrastructure.Services;

/// <summary>
/// Background service that automatically cleans up expired and revoked refresh tokens
/// to prevent database bloat and maintain performance.
/// </summary>
public class RefreshTokenCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RefreshTokenCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24); // Run once per day
    private readonly int _tokenRetentionDays = 30; // Keep tokens for 30 days for audit purposes

    public RefreshTokenCleanupService(
        IServiceProvider serviceProvider,
        ILogger<RefreshTokenCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RefreshTokenCleanupService started. Cleanup will run every {Interval} hours.",
            _cleanupInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                await CleanupExpiredTokensAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Service is stopping, exit gracefully
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during refresh token cleanup");
            }
        }

        _logger.LogInformation("RefreshTokenCleanupService stopped.");
    }

    private async Task CleanupExpiredTokensAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-_tokenRetentionDays);

        _logger.LogInformation(
            "Starting refresh token cleanup. Removing tokens older than {CutoffDate}",
            cutoffDate);

        // Delete tokens that are:
        // 1. Created more than X days ago AND
        // 2. Either expired OR revoked
        var tokensToDelete = await context.RefreshTokens
            .Where(rt => rt.CreatedAt < cutoffDate && (rt.ExpiresAt < DateTime.UtcNow || rt.IsRevoked))
            .ToListAsync(cancellationToken);

        if (tokensToDelete.Any())
        {
            context.RefreshTokens.RemoveRange(tokensToDelete);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Refresh token cleanup completed. Deleted {TokenCount} expired/revoked tokens.",
                tokensToDelete.Count);
        }
        else
        {
            _logger.LogInformation("Refresh token cleanup completed. No tokens to delete.");
        }
    }
}
