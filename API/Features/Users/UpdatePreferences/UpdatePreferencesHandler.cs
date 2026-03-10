using System.Security.Claims;
using API.Infrastructure.Data;
using API.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdatePreferences;

public class UpdatePreferencesHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IValidator<UpdatePreferencesRequest> _validator;

    public UpdatePreferencesHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IValidator<UpdatePreferencesRequest> validator)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _validator = validator;
    }

    public async Task<Result<GetUserPreferencesResponse>> Handle(UpdatePreferencesRequest request)
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Result.Failure<GetUserPreferencesResponse>(UserErrors.Unauthorized);
        }

        var user = await _context.Users
            .Include(u => u.Categories)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return Result.Failure<GetUserPreferencesResponse>(UserErrors.NotFound);
        }

        // Update preferences
        user.DefaultCity = request.DefaultCity;
        user.DefaultCityLatitude = request.DefaultCityLatitude;
        user.DefaultCityLongitude = request.DefaultCityLongitude;
        user.SearchRadius = request.SearchRadius;

        // Update categories
        // Remove existing categories
        var existingCategories = await _context.UserCategories
            .Where(uc => uc.UserId == userId)
            .ToListAsync();
        _context.UserCategories.RemoveRange(existingCategories);

        // Add new categories
        if (request.CategoryIds.Any())
        {
            var newUserCategories = request.CategoryIds.Select(categoryId => new UserCategory
            {
                UserId = userId,
                CategoryId = categoryId
            }).ToList();

            await _context.UserCategories.AddRangeAsync(newUserCategories);
        }

        await _context.SaveChangesAsync();

        // Return updated preferences
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
