using System.Security.Claims;
using API.Infrastructure.Data;
using Shared.Common.Errors;
using Shared.Common.Results;

namespace API.Features.Users.VerifyUser;

public class VerifyUserHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public VerifyUserHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result> Handle()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Result.Failure(UserErrors.Unauthorized);
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        if (user.IsVerified)
        {
            return Result.Failure(new Error("User.AlreadyVerified", "User is already verified"));
        }

        // PLACEHOLDER: Just set verified to true
        // TODO: Implement real BankID integration
        user.IsVerified = true;

        await _context.SaveChangesAsync();

        return Result.Success();
    }
}
