using Shared.Common.Errors;
using Shared.Common.Results;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using API.Models;
using Microsoft.AspNetCore.Identity;
using Shared.Contracts.Auth;

namespace API.Features.Auth.Login;

public class LoginHandler
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoginHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LoginHandler(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ApplicationDbContext context,
        ITokenService tokenService,
        IConfiguration configuration,
        ILogger<LoginHandler> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _tokenService = tokenService;
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<(LoginResponse Response, string AccessToken, string RefreshToken, DateTime RefreshTokenExpiry)>> HandleAsync(LoginRequest request)
    {
        var ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? "Unknown";

        // Find user by email
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning(
                "Login attempt failed: User not found. Email: {Email}, IP: {IP}, UserAgent: {UserAgent}",
                request.Email, ipAddress, userAgent);
            return Result.Failure<(LoginResponse, string, string, DateTime)>(AuthErrors.InvalidCredentials);
        }

        // Check if email is verified
        if (!user.EmailConfirmed)
        {
            _logger.LogWarning(
                "Login attempt failed: Email not verified. UserId: {UserId}, Email: {Email}, IP: {IP}",
                user.Id, request.Email, ipAddress);
            return Result.Failure<(LoginResponse, string, string, DateTime)>(AuthErrors.EmailNotVerified);
        }

        // Check if profile is complete (has password)
        if (!await _userManager.HasPasswordAsync(user))
        {
            _logger.LogWarning(
                "Login attempt failed: Profile not complete. UserId: {UserId}, Email: {Email}, IP: {IP}",
                user.Id, request.Email, ipAddress);
            return Result.Failure<(LoginResponse, string, string, DateTime)>(UserErrors.ProfileNotComplete);
        }

        // Verify password
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            _logger.LogWarning(
                "Login attempt failed: Account locked out. UserId: {UserId}, Email: {Email}, IP: {IP}, UserAgent: {UserAgent}",
                user.Id, request.Email, ipAddress, userAgent);
            return Result.Failure<(LoginResponse, string, string, DateTime)>(UserErrors.LockedOut);
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Login attempt failed: Invalid password. UserId: {UserId}, Email: {Email}, IP: {IP}, UserAgent: {UserAgent}",
                user.Id, request.Email, ipAddress, userAgent);
            return Result.Failure<(LoginResponse, string, string, DateTime)>(AuthErrors.InvalidCredentials);
        }

        // Generate tokens
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Save refresh token with RememberMe awareness
        var refreshTokenExpirationDays = request.RememberMe
            ? int.Parse(_configuration["Jwt:RefreshTokenExpirationDaysRememberMe"]!)
            : int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"]!);

        var refreshTokenEntity = new Models.RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
            IsRevoked = false,
            RememberMe = request.RememberMe
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "User login successful. UserId: {UserId}, Email: {Email}, IP: {IP}, UserAgent: {UserAgent}, RememberMe: {RememberMe}, TokenExpiry: {Days} days",
            user.Id, user.Email, ipAddress, userAgent, request.RememberMe, refreshTokenExpirationDays);

        var response = new LoginResponse(
            user.Id.ToString(),
            user.Email!,
            user.DisplayName
        );

        return Result.Success((response, accessToken, refreshToken, refreshTokenEntity.ExpiresAt));
    }
}
