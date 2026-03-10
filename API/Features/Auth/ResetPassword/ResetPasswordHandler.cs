using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Auth;
using API.Models;

namespace API.Features.Auth.ResetPassword;

public class ResetPasswordHandler
{
    private readonly UserManager<User> _userManager;
    private readonly ILogger<ResetPasswordHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ResetPasswordHandler(
        UserManager<User> userManager,
        ILogger<ResetPasswordHandler> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<ResetPasswordResponse>> Handle(ResetPasswordRequest request)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = httpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown";

        // Find user by reset token
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.PasswordResetToken == request.Token);

        if (user == null)
        {
            _logger.LogWarning("Password reset attempted with invalid token from IP: {IP}, User-Agent: {UserAgent}",
                ipAddress, userAgent);
            return Result.Failure<ResetPasswordResponse>(AuthErrors.PasswordResetTokenInvalid);
        }

        // Check if token has expired
        if (user.PasswordResetTokenExpiry == null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
        {
            _logger.LogWarning("Password reset attempted with expired token for user {UserId} from IP: {IP}, User-Agent: {UserAgent}",
                user.Id, ipAddress, userAgent);

            // Clear expired token
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            await _userManager.UpdateAsync(user);

            return Result.Failure<ResetPasswordResponse>(AuthErrors.PasswordResetTokenExpired);
        }

        // Remove old password and set new one
        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
        {
            _logger.LogError("Failed to remove old password for user {UserId}", user.Id);
            return Result.Failure<ResetPasswordResponse>(
                new Error("USER.PASSWORD_UPDATE_FAILED", "Failed to update password", 500));
        }

        var addResult = await _userManager.AddPasswordAsync(user, request.NewPassword);
        if (!addResult.Succeeded)
        {
            _logger.LogError("Failed to set new password for user {UserId}: {Errors}",
                user.Id, string.Join(", ", addResult.Errors.Select(e => e.Description)));
            return Result.Failure<ResetPasswordResponse>(
                new Error("USER.PASSWORD_UPDATE_FAILED",
                    string.Join(", ", addResult.Errors.Select(e => e.Description)), 400));
        }

        // Clear reset token
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            _logger.LogError("Failed to clear reset token for user {UserId}", user.Id);
        }

        // Update security stamp to invalidate existing tokens
        await _userManager.UpdateSecurityStampAsync(user);

        _logger.LogInformation("Password successfully reset for user {UserId} from IP: {IP}",
            user.Id, ipAddress);

        return Result<ResetPasswordResponse>.Success(new ResetPasswordResponse
        {
            Message = "Password has been reset successfully. You can now log in with your new password."
        });
    }
}
