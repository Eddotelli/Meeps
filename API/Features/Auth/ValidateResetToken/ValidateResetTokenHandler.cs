using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Constants;
using Shared.Common.Results;
using Shared.Contracts.Auth;
using API.Models;

namespace API.Features.Auth.ValidateResetToken;

public class ValidateResetTokenHandler
{
    private readonly UserManager<User> _userManager;
    private readonly ILogger<ValidateResetTokenHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ValidateResetTokenHandler(
        UserManager<User> userManager,
        ILogger<ValidateResetTokenHandler> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<ValidateResetTokenResponse>> Handle(ValidateResetTokenRequest request)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = httpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown";

        // Find user by reset token
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.PasswordResetToken == request.Token);

        // Return same vague error for all invalid scenarios to prevent enumeration attacks
        if (user == null)
        {
            _logger.LogWarning("Password reset token validation failed from IP: {IP}, User-Agent: {UserAgent}",
                ipAddress, userAgent);
            return Result<ValidateResetTokenResponse>.Success(new ValidateResetTokenResponse
            {
                IsValid = false,
                ErrorCode = ErrorCodes.UserPasswordResetTokenInvalid
            });
        }

        // Check if token has expired
        if (user.PasswordResetTokenExpiry == null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
        {
            _logger.LogWarning("Password reset token expired for user {UserId} from IP: {IP}, User-Agent: {UserAgent}",
                user.Id, ipAddress, userAgent);

            // Clear expired token
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            await _userManager.UpdateAsync(user);

            // Return same generic error as invalid token (anti-enumeration)
            return Result<ValidateResetTokenResponse>.Success(new ValidateResetTokenResponse
            {
                IsValid = false,
                ErrorCode = ErrorCodes.UserPasswordResetTokenInvalid
            });
        }

        _logger.LogInformation("Password reset token validated successfully for user {UserId} from IP: {IP}",
            user.Id, ipAddress);

        return Result<ValidateResetTokenResponse>.Success(new ValidateResetTokenResponse
        {
            IsValid = true,
            ErrorCode = null
        });
    }
}
