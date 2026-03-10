using System.Security.Claims;
using API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Users;

namespace API.Features.Users.GetUserProfile;

public class GetUserProfileHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetUserProfileHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<GetUserProfileResponse>> Handle()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Result.Failure<GetUserProfileResponse>(UserErrors.Unauthorized);
        }

        var user = await _context.Users
            .Include(u => u.CreatedEvents)
            .Include(u => u.Events)
            .Include(u => u.Categories)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return Result.Failure<GetUserProfileResponse>(UserErrors.NotFound);
        }

        var response = new GetUserProfileResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email!,
            BirthDate = user.BirthDate,
            Gender = user.Gender,
            Bio = user.Bio,
            ProfileImageUrl = user.ProfileImageUrl,
            CreatedAt = user.CreatedAt,
            IsVerified = user.IsVerified,
            EventsCreated = user.CreatedEvents.Count,
            EventsJoined = user.Events.Count,
            CategoriesCount = user.Categories.Count
        };

        return Result.Success(response);
    }
}
