using Shared.Common.Results;

namespace API.Infrastructure.Services;

/// <summary>
/// Fake email service for testing purposes. Logs emails instead of sending them.
/// Used in Testing environment to avoid sending real emails during automated tests.
/// </summary>
public class FakeEmailService : IEmailService
{
    private readonly ILogger<FakeEmailService> _logger;

    public FakeEmailService(ILogger<FakeEmailService> logger)
    {
        _logger = logger;
    }

    public Task<Result> SendVerificationEmailAsync(string email, string token, string userId)
    {
        _logger.LogInformation(
            "[FAKE EMAIL] Verification email to {Email}\n" +
            "Token: {Token}\n" +
            "UserId: {UserId}\n" +
            "Link: http://localhost:5000/complete-registration?token={Token}",
            email, token, userId, token);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendPasswordResetEmailAsync(string email, string token)
    {
        _logger.LogInformation(
            "[FAKE EMAIL] Password reset email to {Email}\n" +
            "Token: {Token}\n" +
            "Link: http://localhost:5000/reset-password?token={Token}",
            email, token, token);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendWelcomeEmailAsync(string email, string displayName)
    {
        _logger.LogInformation(
            "[FAKE EMAIL] Welcome email to {Email}\n" +
            "DisplayName: {DisplayName}",
            email, displayName);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendEventCancelledEmailAsync(string email, string displayName, string eventTitle, string reason)
    {
        _logger.LogInformation(
            "[FAKE EMAIL] Event Cancelled Email\n" +
            "To: {Email}\n" +
            "Name: {DisplayName}\n" +
            "Event: {EventTitle}\n" +
            "Reason: {Reason}",
            email, displayName, eventTitle, reason);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendAccountDeletedEmailAsync(string email, string displayName, int cancelledEventsCount, int leftEventsCount)
    {
        _logger.LogInformation(
            "[FAKE EMAIL] Account Deleted Email\n" +
            "To: {Email}\n" +
            "Name: {DisplayName}\n" +
            "Cancelled Events: {CancelledEventsCount}\n" +
            "Left Events: {LeftEventsCount}",
            email, displayName, cancelledEventsCount, leftEventsCount);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendEventUpdatedEmailAsync(string email, string displayName, string eventTitle, List<string> changes)
    {
        _logger.LogInformation(
            "[FAKE EMAIL] Event Updated Email\n" +
            "To: {Email}\n" +
            "Name: {DisplayName}\n" +
            "Event: {EventTitle}\n" +
            "Changes: {Changes}",
            email, displayName, eventTitle, string.Join(", ", changes));

        return Task.FromResult(Result.Success());
    }
}
