using System.Security.Claims;
using API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Users;

namespace API.Features.Users.GetUserPreferences;

public class GetUserPreferencesHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetUserPreferencesHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<GetUserPreferencesResponse>> Handle()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Result.Failure<GetUserPreferencesResponse>(UserErrors.Unauthorized);
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return Result.Failure<GetUserPreferencesResponse>(UserErrors.NotFound);
        }

        var categoryIds = await _context.UserCategories
            .Where(uc => uc.UserId == userId)
            .Select(uc => uc.CategoryId)
            .ToArrayAsync();

        var response = new GetUserPreferencesResponse
        {
            DefaultCity = user.DefaultCity,
            DefaultCityLatitude = user.DefaultCityLatitude,
            DefaultCityLongitude = user.DefaultCityLongitude,
            SearchRadius = user.SearchRadius,
            CategoryIds = categoryIds
        };

        return Result.Success(response);
    }
}
