using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Results;
using Shared.Contracts.Auth;
using API.Infrastructure.Services;
using API.Models;
using System.Security.Cryptography;

namespace API.Features.Auth.ForgotPassword;

public class ForgotPasswordHandler
{
    private readonly UserManager<User> _userManager;
    private readonly IEmailService _emailService;
    private readonly ILogger<ForgotPasswordHandler> _logger;

    public ForgotPasswordHandler(
        UserManager<User> userManager,
        IEmailService emailService,
        ILogger<ForgotPasswordHandler> logger)
    {
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<ForgotPasswordResponse>> Handle(ForgotPasswordRequest request)
    {
        // Find user by email
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        // For security reasons, always return success even if user doesn't exist
        // This prevents email enumeration attacks
        if (user == null)
        {
            _logger.LogInformation("Password reset requested for non-existent email: {Email}", request.Email);
            return Result<ForgotPasswordResponse>.Success(new ForgotPasswordResponse
            {
                Message = "If the email exists, a password reset link has been sent."
            });
        }

        // Generate secure reset token (256-bit)
        var resetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        // Token expires in 1 hour (as per authentication policy)
        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            _logger.LogError("Failed to save password reset token for user {UserId}", user.Id);
            return Result<ForgotPasswordResponse>.Success(new ForgotPasswordResponse
            {
                Message = "If the email exists, a password reset link has been sent."
            });
        }

        // Send password reset email
        var emailResult = await _emailService.SendPasswordResetEmailAsync(user.Email!, resetToken);
        if (emailResult.IsFailure)
        {
            _logger.LogWarning("Failed to send password reset email to {Email}", user.Email);
        }

        _logger.LogInformation("Password reset token generated for user {UserId}", user.Id);

        return Result<ForgotPasswordResponse>.Success(new ForgotPasswordResponse
        {
            Message = "If the email exists, a password reset link has been sent."
        });
    }
}
