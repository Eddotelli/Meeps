using System.Security.Claims;
using API.Infrastructure.Data;
using API.Models;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdatePassword;

public class UpdatePasswordHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<User> _userManager;
    private readonly IValidator<UpdatePasswordRequest> _validator;

    public UpdatePasswordHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        UserManager<User> userManager,
        IValidator<UpdatePasswordRequest> validator)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _validator = validator;
    }

    public async Task<Result> Handle(UpdatePasswordRequest request)
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

        // Verify current password
        var passwordCheck = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
        if (!passwordCheck)
        {
            return Result.Failure(UserErrors.InvalidPassword);
        }

        // Change password
        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return Result.Failure(UserErrors.PasswordChangeFailed);
        }

        return Result.Success();
    }
}
