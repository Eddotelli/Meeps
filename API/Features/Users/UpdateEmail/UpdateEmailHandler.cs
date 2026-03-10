using System.Security.Claims;
using API.Infrastructure.Data;
using API.Models;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdateEmail;

public class UpdateEmailHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<User> _userManager;
    private readonly IValidator<UpdateEmailRequest> _validator;

    public UpdateEmailHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        UserManager<User> userManager,
        IValidator<UpdateEmailRequest> validator)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _validator = validator;
    }

    public async Task<Result> Handle(UpdateEmailRequest request)
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Result.Failure(UserErrors.Unauthorized);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        // Verify password
        var passwordCheck = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordCheck)
        {
            return Result.Failure(UserErrors.InvalidPassword);
        }

        // Check if email already exists
        var emailExists = await _context.Users.AnyAsync(u => u.Email == request.NewEmail && u.Id != userId);
        if (emailExists)
        {
            return Result.Failure(UserErrors.EmailAlreadyExists);
        }

        // Update email
        var result = await _userManager.SetEmailAsync(user, request.NewEmail);
        if (!result.Succeeded)
        {
            return Result.Failure(UserErrors.EmailChangeFailed);
        }

        // Update username to match email
        await _userManager.SetUserNameAsync(user, request.NewEmail);

        return Result.Success();
    }
}
