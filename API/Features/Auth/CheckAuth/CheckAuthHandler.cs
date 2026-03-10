using System.Security.Claims;
using API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Results;
using Shared.Contracts.Auth;

namespace API.Features.Auth.CheckAuth;

/// <summary>
/// Handler for checking authentication status and returning user info.
/// </summary>
public class CheckAuthHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _context;

    public CheckAuthHandler(IHttpContextAccessor httpContextAccessor, ApplicationDbContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public async Task<Result<CheckAuthResponse>> Handle()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return Result.Failure<CheckAuthResponse>(new Error("AUTH.NOT_AUTHENTICATED", "User is not authenticated", 401));
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
        {
            return Result.Failure<CheckAuthResponse>(new Error("AUTH.INVALID_CLAIMS", "Invalid user claims", 401));
        }

        // Fetch current DisplayName from database to ensure it's always up-to-date
        var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == int.Parse(userId));

        if (dbUser == null)
        {
            return Result.Failure<CheckAuthResponse>(new Error("AUTH.USER_NOT_FOUND", "User not found", 401));
        }

        var response = new CheckAuthResponse
        {
            UserId = int.Parse(userId),
            Email = email,
            DisplayName = dbUser.DisplayName
        };

        return Result.Success(response);
    }
}
