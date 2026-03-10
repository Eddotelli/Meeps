using API.Infrastructure.Data;
using API.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Common.Results;
using System.Threading;

namespace API.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Sets up an in-memory database and mocks external services.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<API.Program>
{
    private static int _databaseCounter = 0;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment to Testing - will automatically load appsettings.Testing.json
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace IEmailService with a fake implementation
            services.RemoveAll<IEmailService>();
            services.AddScoped<IEmailService, FakeEmailService>();

            // Replace ILocationDetectionService with a fake implementation
            services.RemoveAll<ILocationDetectionService>();
            services.AddScoped<ILocationDetectionService, FakeLocationDetectionService>();

            // Remove existing DbContext configuration
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Add DbContext using a unique InMemory database for each test instance
            var dbName = $"TestDb_{Interlocked.Increment(ref _databaseCounter)}";
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
            });
        });
    }
}

/// <summary>
/// Fake email service for testing.
/// Doesn't actually send emails, just logs the action.
/// </summary>
public class FakeEmailService : IEmailService
{
    public List<EmailLog> SentEmails { get; } = new();

    public Task<Result> SendVerificationEmailAsync(string toEmail, string verificationToken, string userId)
    {
        SentEmails.Add(new EmailLog
        {
            ToEmail = toEmail,
            Subject = "Verification",
            Token = verificationToken,
            SentAt = DateTime.UtcNow
        });
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendPasswordResetEmailAsync(string toEmail, string resetToken)
    {
        SentEmails.Add(new EmailLog
        {
            ToEmail = toEmail,
            Subject = "Password Reset",
            Token = resetToken,
            SentAt = DateTime.UtcNow
        });
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendWelcomeEmailAsync(string email, string displayName)
    {
        SentEmails.Add(new EmailLog
        {
            ToEmail = email,
            Subject = "Welcome",
            Token = string.Empty,
            SentAt = DateTime.UtcNow
        });
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendEventCancelledEmailAsync(string email, string displayName, string eventTitle, string reason)
    {
        SentEmails.Add(new EmailLog
        {
            ToEmail = email,
            Subject = "Event Cancelled",
            Token = string.Empty,
            SentAt = DateTime.UtcNow
        });
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendEventUpdatedEmailAsync(string email, string displayName, string eventTitle, List<string> changes)
    {
        SentEmails.Add(new EmailLog
        {
            ToEmail = email,
            Subject = "Event Updated",
            Token = string.Empty,
            SentAt = DateTime.UtcNow
        });
        return Task.FromResult(Result.Success());
    }

    public Task<Result> SendAccountDeletedEmailAsync(string email, string displayName, int cancelledEventsCount, int leftEventsCount)
    {
        SentEmails.Add(new EmailLog
        {
            ToEmail = email,
            Subject = "Account Deleted",
            Token = string.Empty,
            SentAt = DateTime.UtcNow
        });
        return Task.FromResult(Result.Success());
    }

    public class EmailLog
    {
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
    }
}

/// <summary>
/// Fake location detection service for testing.
/// Returns Stockholm as default location.
/// </summary>
public class FakeLocationDetectionService : ILocationDetectionService
{
    public Task<Shared.Contracts.Locations.LocationSearchResult?> DetectLocationFromIP(string? ipAddress)
    {
        // Return Stockholm as default test location
        var result = new Shared.Contracts.Locations.LocationSearchResult
        {
            City = "Stockholm",
            Country = "Sweden",
            Latitude = 59.3293,
            Longitude = 18.0686
        };

        return Task.FromResult<Shared.Contracts.Locations.LocationSearchResult?>(result);
    }
}
