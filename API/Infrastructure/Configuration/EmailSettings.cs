namespace API.Infrastructure.Configuration;

public class EmailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public EnabledEmails EnabledEmails { get; set; } = new();
}

public class EnabledEmails
{
    public bool VerifyEmail { get; set; } = true;
    public bool PasswordReset { get; set; } = true;
    public bool WelcomeEmail { get; set; } = true;
    public bool EventInvitation { get; set; } = true;
    public bool EventReminder { get; set; } = true;
    public bool EventCancellation { get; set; } = true;
    public bool AccountDeletion { get; set; } = true;
}
