using Shared.Common.Errors;
using Shared.Common.Results;
using API.Infrastructure.Services;
using API.Models;
using Microsoft.AspNetCore.Identity;
using Shared.Contracts.Auth;

namespace API.Features.Auth.Register;

public class RegisterHandler
{
    private readonly UserManager<User> _userManager;
    private readonly IEmailService _emailService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RegisterHandler> _logger;

    public RegisterHandler(
        UserManager<User> userManager,
        IEmailService emailService,
        ITokenService tokenService,
        ILogger<RegisterHandler> logger)
    {
        _userManager = userManager;
        _emailService = emailService;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Result<RegisterResponse>> HandleAsync(RegisterRequest request)
    {
        // Check if email already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            // Always return error if user exists (whether verified or not)
            // This prevents information disclosure about which emails are registered
            return Result.Failure<RegisterResponse>(UserErrors.EmailAlreadyExists);
        }

        // Create new user with only email (temporary username)
        var user = new User
        {
            Email = request.Email,
            UserName = request.Email, // Temporary, will be updated later
            EmailConfirmed = false,
            EmailVerificationToken = _tokenService.GenerateEmailVerificationToken(),
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow
        };

        // Create user without password (will be set during profile completion)
        var result = await _userManager.CreateAsync(user);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create user: {Errors}", errors);
            return Result.Failure<RegisterResponse>(UserErrors.RegistrationFailed);
        }

        // Send verification email
        var emailResult = await _emailService.SendVerificationEmailAsync(
            user.Email!,
            user.EmailVerificationToken!,
            user.Id.ToString()
        );

        if (emailResult.IsFailure)
        {
            // Rollback user creation if email fails
            await _userManager.DeleteAsync(user);
            return Result.Failure<RegisterResponse>(emailResult.Error!);
        }

        _logger.LogInformation("User registered successfully: {Email}", user.Email);

        return Result.Success(new RegisterResponse(
            "A verification email has been sent to your email address. Please check your inbox.",
            user.Id.ToString()
        ));
    }
}
