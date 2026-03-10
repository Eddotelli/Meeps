using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Enums;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using API.Infrastructure.Configuration;

namespace API.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IConfiguration configuration,
        IOptions<EmailSettings> emailSettings,
        ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task<Result> SendVerificationEmailAsync(string email, string token, string userId)
    {
        if (!IsEmailEnabled(EmailType.VerifyEmail))
        {
            _logger.LogInformation("Email type {EmailType} is disabled. Skipping email to {Email}",
                EmailType.VerifyEmail, email);
            return Result.Success();
        }

        try
        {
            var appUrl = _configuration["AppUrl"] ?? "https://localhost:7000";
            var verificationLink = $"{appUrl}/complete-registration?token={token}";

            var subject = "Verifiera din email - Meeps";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>Välkommen till Meeps!</h2>
                    <p>Klicka på länken nedan för att verifiera din email-adress och slutföra din registrering:</p>
                    <p><a href='{verificationLink}' style='background-color: #4CAF50; color: white; padding: 14px 20px; text-align: center; text-decoration: none; display: inline-block; border-radius: 4px;'>Slutför Registrering</a></p>
                    <p>Eller kopiera och klistra in denna länk i din webbläsare:</p>
                    <p>{verificationLink}</p>
                    <p>Denna länk är giltig i 24 timmar.</p>
                    <br>
                    <p>Om du inte skapat ett konto hos oss, ignorera detta meddelande.</p>
                </body>
                </html>
            ";

            return await SendEmailAsync(email, subject, body, EmailType.VerifyEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", email);
            return Result.Failure(EmailErrors.SendFailed);
        }
    }

    public async Task<Result> SendPasswordResetEmailAsync(string email, string token)
    {
        if (!IsEmailEnabled(EmailType.PasswordReset))
        {
            _logger.LogInformation("Email type {EmailType} is disabled. Skipping email to {Email}",
                EmailType.PasswordReset, email);
            return Result.Success();
        }

        try
        {
            var appUrl = _configuration["AppUrl"] ?? "https://localhost:7000";
            // URL-encode token to handle Base64 special characters (+, /, =)
            var encodedToken = Uri.EscapeDataString(token);
            var resetLink = $"{appUrl}/reset-password?token={encodedToken}";

            var subject = "Återställ ditt lösenord - Meeps";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>Återställ ditt lösenord</h2>
                    <p>Klicka på länken nedan för att återställa ditt lösenord:</p>
                    <p><a href='{resetLink}' style='background-color: #4CAF50; color: white; padding: 14px 20px; text-align: center; text-decoration: none; display: inline-block; border-radius: 4px;'>Återställ Lösenord</a></p>
                    <p>Eller kopiera och klistra in denna länk i din webbläsare:</p>
                    <p>{resetLink}</p>
                    <p>Denna länk är giltig i 1 timme.</p>
                    <br>
                    <p>Om du inte begärt återställning av lösenord, ignorera detta meddelande.</p>
                </body>
                </html>
            ";

            return await SendEmailAsync(email, subject, body, EmailType.PasswordReset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
            return Result.Failure(EmailErrors.SendFailed);
        }
    }

    public async Task<Result> SendWelcomeEmailAsync(string email, string displayName)
    {
        if (!IsEmailEnabled(EmailType.WelcomeEmail))
        {
            _logger.LogInformation("Email type {EmailType} is disabled. Skipping email to {Email}",
                EmailType.WelcomeEmail, email);
            return Result.Success();
        }

        try
        {
            var subject = "Välkommen till Meeps!";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>Välkommen {displayName}!</h2>
                    <p>Ditt konto är nu aktiverat och du kan börja använda Meeps.</p>
                    <p>Börja med att utforska events i din närhet eller skapa dina egna!</p>
                    <br>
                    <p>Vi hoppas du kommer ha en fantastisk upplevelse!</p>
                    <p>- Meeps-teamet</p>
                </body>
                </html>
            ";

            return await SendEmailAsync(email, subject, body, EmailType.WelcomeEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", email);
            return Result.Failure(EmailErrors.SendFailed);
        }
    }

    public async Task<Result> SendEventCancelledEmailAsync(string email, string displayName, string eventTitle, string reason)
    {
        if (!IsEmailEnabled(EmailType.EventCancellation))
        {
            _logger.LogInformation("Email type {EmailType} is disabled. Skipping email to {Email}",
                EmailType.EventCancellation, email);
            return Result.Success();
        }

        try
        {
            var subject = $"Event Cancelled: {eventTitle}";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #d32f2f;'>Event Cancelled</h2>
                    <p>Hello {displayName},</p>
                    <p>We regret to inform you that the following event has been cancelled:</p>
                    <div style='background-color: #f5f5f5; padding: 15px; border-left: 4px solid #d32f2f; margin: 20px 0;'>
                        <h3 style='margin-top: 0;'>{eventTitle}</h3>
                        <p><strong>Reason:</strong> {reason}</p>
                    </div>
                    <p>We apologize for any inconvenience this may cause.</p>
                    <p>You can browse other available events in your area on the Meeps app.</p>
                    <br>
                    <p>Best regards,<br>The Meeps Team</p>
                </body>
                </html>
            ";

            return await SendEmailAsync(email, subject, body, EmailType.EventCancellation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event cancelled email to {Email}", email);
            return Result.Failure(EmailErrors.SendFailed);
        }
    }

    public async Task<Result> SendAccountDeletedEmailAsync(string email, string displayName, int cancelledEventsCount, int leftEventsCount)
    {
        if (!IsEmailEnabled(EmailType.AccountDeletion))
        {
            _logger.LogInformation("Email type {EmailType} is disabled. Skipping email to {Email}",
                EmailType.AccountDeletion, email);
            return Result.Success();
        }

        try
        {
            var subject = "Account Deleted - Meeps";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #d32f2f;'>Account Deletion Confirmation</h2>
                    <p>Hello {displayName},</p>
                    <p>This email confirms that your Meeps account has been permanently deleted.</p>
                    <div style='background-color: #f5f5f5; padding: 15px; border-left: 4px solid #1976d2; margin: 20px 0;'>
                        <h3 style='margin-top: 0;'>Account Summary:</h3>
                        <ul>
                            <li><strong>Cancelled events:</strong> {cancelledEventsCount}</li>
                            <li><strong>Left events:</strong> {leftEventsCount}</li>
                        </ul>
                    </div>
                    <p>What has been deleted:</p>
                    <ul>
                        <li>Your profile information</li>
                        <li>All events you created (cancelled)</li>
                        <li>Your participation in other events</li>
                    </ul>
                    <p>What has been preserved:</p>
                    <ul>
                        <li>Your messages in event chats (anonymized as ""Deleted User"")</li>
                    </ul>
                    <br>
                    <p>Thank you for being part of the Meeps community!</p>
                    <p>Best regards,<br>The Meeps Team</p>
                </body>
                </html>
            ";

            return await SendEmailAsync(email, subject, body, EmailType.AccountDeletion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send account deleted email to {Email}", email);
            return Result.Failure(EmailErrors.SendFailed);
        }
    }

    public async Task<Result> SendEventUpdatedEmailAsync(string email, string displayName, string eventTitle, List<string> changes)
    {
        if (!IsEmailEnabled(EmailType.EventCancellation)) // Using EventCancellation as proxy for event updates
        {
            _logger.LogInformation("Email type {EmailType} is disabled. Skipping event update email to {Email}",
                EmailType.EventCancellation, email);
            return Result.Success();
        }

        try
        {
            var changesHtml = string.Join("", changes.Select(c => $"<li>{c}</li>"));
            var appUrl = _configuration["AppUrl"] ?? "https://localhost:7000";

            var subject = $"Event Updated: {eventTitle}";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #1976d2;'>Event Updated</h2>
                    <p>Hello {displayName},</p>
                    <p>The event you're participating in has been updated:</p>
                    <div style='background-color: #f5f5f5; padding: 15px; border-left: 4px solid #1976d2; margin: 20px 0;'>
                        <h3 style='margin-top: 0;'>{eventTitle}</h3>
                        <p><strong>Changes made:</strong></p>
                        <ul>
                            {changesHtml}
                        </ul>
                    </div>
                    <p>Please review the updated event details in the Meeps app.</p>
                    <p><a href='{appUrl}' style='background-color: #1976d2; color: white; padding: 10px 20px; text-align: center; text-decoration: none; display: inline-block; border-radius: 4px; margin: 10px 0;'>View Event</a></p>
                    <br>
                    <p>Best regards,<br>The Meeps Team</p>
                </body>
                </html>
            ";

            return await SendEmailAsync(email, subject, body, EmailType.EventCancellation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send event updated email to {Email}", email);
            return Result.Failure(EmailErrors.SendFailed);
        }
    }

    private async Task<Result> SendEmailAsync(string toEmail, string subject, string htmlBody, EmailType emailType)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _emailSettings.FromName,
                _emailSettings.From
            ));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _emailSettings.Host,
                _emailSettings.Port,
                SecureSocketOptions.StartTls
            );

            await client.AuthenticateAsync(
                _emailSettings.Username,
                _emailSettings.Password
            );

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email type {EmailType} sent successfully to {Email}", emailType, toEmail);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {EmailType} email to {Email}", emailType, toEmail);
            return Result.Failure(EmailErrors.SendFailed);
        }
    }

    private bool IsEmailEnabled(EmailType emailType)
    {
        return emailType switch
        {
            EmailType.VerifyEmail => _emailSettings.EnabledEmails.VerifyEmail,
            EmailType.PasswordReset => _emailSettings.EnabledEmails.PasswordReset,
            EmailType.WelcomeEmail => _emailSettings.EnabledEmails.WelcomeEmail,
            EmailType.EventInvitation => _emailSettings.EnabledEmails.EventInvitation,
            EmailType.EventReminder => _emailSettings.EnabledEmails.EventReminder,
            EmailType.EventCancellation => _emailSettings.EnabledEmails.EventCancellation,
            EmailType.AccountDeletion => _emailSettings.EnabledEmails.AccountDeletion,
            _ => false
        };
    }
}
