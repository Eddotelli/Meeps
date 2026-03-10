#if DEBUG
using API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Test;

/// <summary>
/// Test-only endpoints for development and automated testing.
/// These endpoints are ONLY available in DEBUG builds and will not exist in Release/Production.
/// </summary>
public static class TestEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/test")
            .WithTags("Test")
            .WithOpenApi();

        group.MapGet("/verification-token/{email}", GetVerificationToken)
            .WithName("GetVerificationToken")
            .WithSummary("Get verification token for a user (TEST ONLY)")
            .Produces<string>(200)
            .Produces(404);

        group.MapGet("/reset-token/{email}", GetResetToken)
            .WithName("GetResetToken")
            .WithSummary("Get password reset token for a user (TEST ONLY)")
            .Produces<string>(200)
            .Produces(404);

        group.MapDelete("/reset-user/{email}", ResetUser)
            .WithName("ResetUser")
            .WithSummary("Delete a user by email (TEST ONLY)")
            .Produces(200)
            .Produces(404);

        group.MapDelete("/reset-database", ResetDatabase)
            .WithName("ResetDatabase")
            .WithSummary("Delete all test users (TEST ONLY)")
            .Produces(200);
    }

    private static async Task<IResult> GetVerificationToken(
        string email,
        ApplicationDbContext context,
        ILogger<Program> logger)
    {
        logger.LogInformation("TEST: Getting verification token for {Email}", email);

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            logger.LogWarning("TEST: User not found: {Email}", email);
            return Results.NotFound(new { error = "User not found" });
        }

        if (string.IsNullOrEmpty(user.EmailVerificationToken))
        {
            logger.LogWarning("TEST: No verification token for {Email}", email);
            return Results.NotFound(new { error = "No verification token found" });
        }

        logger.LogInformation("TEST: Returning token for {Email}: {Token}", email, user.EmailVerificationToken);
        return Results.Ok(new { token = user.EmailVerificationToken });
    }

    private static async Task<IResult> GetResetToken(
        string email,
        ApplicationDbContext context,
        ILogger<Program> logger)
    {
        logger.LogInformation("TEST: Getting reset token for {Email}", email);

        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            logger.LogWarning("TEST: User not found: {Email}", email);
            return Results.NotFound(new { error = "User not found" });
        }

        if (string.IsNullOrEmpty(user.PasswordResetToken))
        {
            logger.LogWarning("TEST: No reset token for {Email}", email);
            return Results.NotFound(new { error = "No reset token found" });
        }

        logger.LogInformation("TEST: Returning reset token for {Email}: {Token}", email, user.PasswordResetToken);
        return Results.Ok(new { token = user.PasswordResetToken });
    }

    private static async Task<IResult> ResetUser(
        string email,
        ApplicationDbContext context,
        ILogger<Program> logger)
    {
        logger.LogInformation("TEST: Resetting user {Email}", email);

        var user = await context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            logger.LogWarning("TEST: User not found: {Email}", email);
            return Results.NotFound(new { error = "User not found" });
        }

        context.Users.Remove(user);
        await context.SaveChangesAsync();

        logger.LogInformation("TEST: User {Email} deleted successfully", email);
        return Results.Ok(new { message = $"User {email} deleted successfully" });
    }

    private static async Task<IResult> ResetDatabase(
        ApplicationDbContext context,
        ILogger<Program> logger)
    {
        logger.LogInformation("TEST: Resetting database (deleting test users)");

        var deletedCount = await context.Users
            .Where(u => u.Email != null && u.Email.StartsWith("test"))
            .ExecuteDeleteAsync();

        logger.LogInformation("TEST: Deleted {Count} test users", deletedCount);
        return Results.Ok(new { message = $"Deleted {deletedCount} test users" });
    }
}
#endif
