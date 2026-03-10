using System.Security.Claims;
using API.Infrastructure.Data;
using API.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdateCategories;

public class UpdateCategoriesHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UpdateCategoriesHandler> _logger;

    public UpdateCategoriesHandler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<UpdateCategoriesHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<Result<UpdateCategoriesResponse>> Handle(UpdateCategoriesRequest request)
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Result.Failure<UpdateCategoriesResponse>(UserErrors.Unauthorized);
        }

        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            return Result.Failure<UpdateCategoriesResponse>(UserErrors.NotFound);
        }

        _logger.LogInformation("Updating categories for user {UserId}: {CategoryIds}",
            userId, string.Join(", ", request.CategoryIds));

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

        _logger.LogInformation("Categories updated successfully for user {UserId}", userId);

        // Get updated categories
        var categoryIds = await _context.UserCategories
            .Where(uc => uc.UserId == userId)
            .Select(uc => uc.CategoryId)
            .ToArrayAsync();

        var response = new UpdateCategoriesResponse
        {
            CategoryIds = categoryIds
        };

        return Result.Success(response);
    }
}
