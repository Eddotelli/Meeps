using Shared.Common.Errors;
using Shared.Common.Results;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Auth;
using Shared.Enums;

namespace API.Features.Auth.CompleteRegistration;

public class CompleteRegistrationHandler
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CompleteRegistrationHandler> _logger;
    private readonly ILocationDetectionService _locationDetectionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CompleteRegistrationHandler(
        UserManager<User> userManager,
        ApplicationDbContext context,
        ITokenService tokenService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<CompleteRegistrationHandler> logger,
        ILocationDetectionService locationDetectionService,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _context = context;
        _tokenService = tokenService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
        _locationDetectionService = locationDetectionService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<(CompleteRegistrationResponse Response, string AccessToken, string RefreshToken, DateTime RefreshTokenExpiry)>> HandleAsync(CompleteRegistrationRequest request)
    {
        // Find user by verification token
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.EmailVerificationToken == request.VerificationToken);

        if (user == null)
        {
            return Result.Failure<(CompleteRegistrationResponse, string, string, DateTime)>(EmailErrors.InvalidToken);
        }

        // Check if token has expired
        if (user.EmailVerificationTokenExpiry == null || user.EmailVerificationTokenExpiry < DateTime.UtcNow)
        {
            return Result.Failure<(CompleteRegistrationResponse, string, string, DateTime)>(EmailErrors.InvalidToken);
        }

        // Check if email is verified
        if (!user.EmailConfirmed)
        {
            return Result.Failure<(CompleteRegistrationResponse, string, string, DateTime)>(AuthErrors.EmailNotVerified);
        }

        // Check if profile is already complete (user has password)
        if (await _userManager.HasPasswordAsync(user))
        {
            return Result.Failure<(CompleteRegistrationResponse, string, string, DateTime)>(UserErrors.ProfileAlreadyComplete);
        }

        // Check if display name is already taken
        var displayNameExists = await _context.Users
            .AnyAsync(u => u.DisplayName == request.DisplayName && u.Id != user.Id);
        if (displayNameExists)
        {
            return Result.Failure<(CompleteRegistrationResponse, string, string, DateTime)>(UserErrors.DisplayNameAlreadyExists);
        }

        // Set password
        var passwordResult = await _userManager.AddPasswordAsync(user, request.Password);
        if (!passwordResult.Succeeded)
        {
            var errors = string.Join(", ", passwordResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to set password for user {UserId}: {Errors}", user.Id, errors);
            return Result.Failure<(CompleteRegistrationResponse, string, string, DateTime)>(
                UserErrors.PasswordSetFailed with { Detail = errors }
            );
        }

        // Update profile
        user.DisplayName = request.DisplayName;
        user.UserName = request.DisplayName; // Use display name as username
        user.BirthDate = request.BirthDate;
        user.Gender = request.Gender;
        user.AcceptedTerms = request.AcceptTerms;
        user.IsVerified = false; // Will be true when verified via BankID
        user.EmailVerificationToken = null; // Clear verification token
        user.EmailVerificationTokenExpiry = null;

        // Detect and set default location from IP address
        var ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var detectedLocation = await _locationDetectionService.DetectLocationFromIP(ipAddress);

        if (detectedLocation != null)
        {
            user.DefaultCity = detectedLocation.City;
            user.DefaultCityLatitude = detectedLocation.Latitude;
            user.DefaultCityLongitude = detectedLocation.Longitude;

            _logger.LogInformation("Set default location for user {UserId}: {City} (IP: {IP})",
                user.Id, detectedLocation.City, ipAddress ?? "unknown");
        }
        else
        {
            _logger.LogWarning("Could not detect location for user {UserId}, no default location set", user.Id);
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to update user profile {UserId}: {Errors}", user.Id, errors);
            return Result.Failure<(CompleteRegistrationResponse, string, string, DateTime)>(UserErrors.UpdateFailed);
        }

        // Assign default User role
        var roleResult = await _userManager.AddToRoleAsync(user, UserRole.User.ToString());
        if (!roleResult.Succeeded)
        {
            var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
            _logger.LogError("Failed to assign role to user {UserId}: {Errors}", user.Id, errors);
            return Result.Failure<(CompleteRegistrationResponse, string, string, DateTime)>(UserErrors.UpdateFailed);
        }

        _logger.LogInformation("Assigned User role to user {UserId}", user.Id);

        // Add user categories
        var userCategories = request.CategoryIds
            .Select(categoryId => new UserCategory
            {
                UserId = user.Id,
                CategoryId = categoryId
            })
            .ToList();

        _context.UserCategories.AddRange(userCategories);
        await _context.SaveChangesAsync();

        // Generate tokens
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Save refresh token (no RememberMe on complete registration, use default expiry)
        var refreshTokenExpirationDays = int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"]!);
        var refreshTokenEntity = new Models.RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
            IsRevoked = false,
            RememberMe = true  // Default to true on registration for better UX
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        // Send welcome email (fire and forget)
        _ = _emailService.SendWelcomeEmailAsync(user.Email!, user.DisplayName);

        _logger.LogInformation("Registration completed successfully for user {UserId}", user.Id);

        var response = new CompleteRegistrationResponse(
            user.Id.ToString(),
            user.Email!,
            user.DisplayName
        );

        return Result.Success((response, accessToken, refreshToken, refreshTokenEntity.ExpiresAt));
    }
}
