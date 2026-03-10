using System.Security.Claims;
using API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdateLocation;

public class UpdateLocationHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UpdateLocationHandler> _logger;

    public UpdateLocationHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<UpdateLocationHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<Result<UpdateLocationResponse>> Handle(UpdateLocationRequest request)
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Result.Failure<UpdateLocationResponse>(UserErrors.Unauthorized);
        }

        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            return Result.Failure<UpdateLocationResponse>(UserErrors.NotFound);
        }

        _logger.LogInformation("Updating location for user {UserId}: City={City}, Lat={Lat}, Lon={Lon}, Radius={Radius}",
            userId, request.DefaultCity, request.DefaultCityLatitude, request.DefaultCityLongitude, request.SearchRadius);

        // Update location preferences
        user.DefaultCity = request.DefaultCity;
        user.DefaultCityLatitude = request.DefaultCityLatitude;
        user.DefaultCityLongitude = request.DefaultCityLongitude;
        user.SearchRadius = request.SearchRadius;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Location updated successfully for user {UserId}: {City}", userId, user.DefaultCity);

        var response = new UpdateLocationResponse
        {
            DefaultCity = user.DefaultCity,
            DefaultCityLatitude = user.DefaultCityLatitude,
            DefaultCityLongitude = user.DefaultCityLongitude,
            SearchRadius = user.SearchRadius
        };

        return Result.Success(response);
    }
}
