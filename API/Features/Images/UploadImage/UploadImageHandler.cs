using System.Security.Claims;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Images;
using Shared.Enums;

namespace API.Features.Images.UploadImage;

public class UploadImageHandler
{
    private readonly IImageService _imageService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UploadImageHandler> _logger;

    public UploadImageHandler(
        IImageService imageService,
        IHttpContextAccessor httpContextAccessor,
        ApplicationDbContext context,
        ILogger<UploadImageHandler> logger)
    {
        _imageService = imageService;
        _httpContextAccessor = httpContextAccessor;
        _context = context;
        _logger = logger;
    }

    public async Task<Result<ImageResponse>> HandleAsync(IFormFile file, string context, int? eventId = null)
    {
        // Get current user ID
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Unauthorized image upload attempt");
            return Result.Failure<ImageResponse>(AuthErrors.InvalidCredentials);
        }

        // Parse context
        if (!Enum.TryParse<ImageContext>(context, true, out var imageContext))
        {
            _logger.LogWarning("Invalid image context: {Context}", context);
            return Result.Failure<ImageResponse>(ImageErrors.InvalidContext);
        }

        _logger.LogInformation("User {UserId} uploading image for context: {Context}, eventId: {EventId}", userId, imageContext, eventId);

        // Upload image
        var result = await _imageService.UploadImageAsync(file, userId, imageContext, eventId);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Image uploaded successfully for user {UserId}: {ImageUrl}",
                userId, result.Value.ImageUrl);

            // If uploading profile image, update User.ProfileImageUrl in database
            if (imageContext == ImageContext.Profile)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.ProfileImageUrl = result.Value.ImageUrl;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated ProfileImageUrl for user {UserId}", userId);
                }
            }
        }

        return result;
    }
}
