using Shared.Common.Errors;
using Shared.Common.Results;
using API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Auth;

namespace API.Features.Auth.VerifyEmail;

public class VerifyEmailHandler
{
    private readonly UserManager<User> _userManager;
    private readonly ILogger<VerifyEmailHandler> _logger;

    public VerifyEmailHandler(
        UserManager<User> userManager,
        ILogger<VerifyEmailHandler> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<Result<VerifyEmailResponse>> HandleAsync(VerifyEmailRequest request)
    {
        // Find user by token (more flexible - can work with or without userId)
        User? user = null;

        if (!string.IsNullOrEmpty(request.UserId))
        {
            // Try to find by ID first if provided
            user = await _userManager.FindByIdAsync(request.UserId);

            // Validate token matches
            if (user != null && (string.IsNullOrEmpty(user.EmailVerificationToken) ||
                user.EmailVerificationToken != request.Token))
            {
                return Result.Failure<VerifyEmailResponse>(EmailErrors.InvalidToken);
            }
        }

        // If user not found by ID or ID not provided, find by token
        if (user == null)
        {
            user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.EmailVerificationToken == request.Token);
        }

        if (user == null)
        {
            return Result.Failure<VerifyEmailResponse>(EmailErrors.InvalidToken);
        }

        // Check if already verified
        if (user.EmailConfirmed)
        {
            return Result.Failure<VerifyEmailResponse>(EmailErrors.AlreadyVerified);
        }

        // Check if token has expired
        if (user.EmailVerificationTokenExpiry == null ||
            user.EmailVerificationTokenExpiry < DateTime.UtcNow)
        {
            return Result.Failure<VerifyEmailResponse>(EmailErrors.InvalidToken);
        }

        // Mark email as confirmed
        user.EmailConfirmed = true;
        // Keep token for CompleteRegistration endpoint
        // Token will be cleared when registration is completed

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to verify email for user {UserId}: {Errors}", user.Id, errors);
            return Result.Failure<VerifyEmailResponse>(UserErrors.UpdateFailed);
        }

        _logger.LogInformation("Email verified successfully for user {UserId}", user.Id);

        return Result.Success(new VerifyEmailResponse(
            "Email verified successfully! You can now complete your profile.",
            user.Id.ToString(),
            user.Email!
        ));
    }
}
