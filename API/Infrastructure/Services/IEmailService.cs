using Shared.Common.Results;

namespace API.Infrastructure.Services;

public interface IEmailService
{
    Task<Result> SendVerificationEmailAsync(string email, string token, string userId);
    Task<Result> SendPasswordResetEmailAsync(string email, string token);
    Task<Result> SendWelcomeEmailAsync(string email, string displayName);
    Task<Result> SendEventCancelledEmailAsync(string email, string displayName, string eventTitle, string reason);
    Task<Result> SendAccountDeletedEmailAsync(string email, string displayName, int cancelledEventsCount, int leftEventsCount);
    Task<Result> SendEventUpdatedEmailAsync(string email, string displayName, string eventTitle, List<string> changes);
}
