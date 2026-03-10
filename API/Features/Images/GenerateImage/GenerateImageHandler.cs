using System.Security.Claims;
using API.Infrastructure.Data;
using API.Infrastructure.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Contracts.Images;
using Shared.Enums;

namespace API.Features.Images.GenerateImage;

public class GenerateImageHandler
{
    private readonly IImageService _imageService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _context;
    private readonly IValidator<GenerateImageRequest> _validator;
    private readonly ILogger<GenerateImageHandler> _logger;

    public GenerateImageHandler(
        IImageService imageService,
        IHttpContextAccessor httpContextAccessor,
        ApplicationDbContext context,
        IValidator<GenerateImageRequest> validator,
        ILogger<GenerateImageHandler> logger)
    {
        _imageService = imageService;
        _httpContextAccessor = httpContextAccessor;
        _context = context;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<ImageResponse>> HandleAsync(GenerateImageRequest request)
    {
        // Get current user ID
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Unauthorized image generation attempt");
            return Result.Failure<ImageResponse>(AuthErrors.InvalidCredentials);
        }

        // Parse context
        if (!Enum.TryParse<ImageContext>(request.Context, true, out var imageContext))
        {
            _logger.LogWarning("Invalid image context: {Context}", request.Context);
            return Result.Failure<ImageResponse>(ImageErrors.InvalidContext);
        }

        _logger.LogInformation(
            "User {UserId} generating image for context: {Context}, prompt: {Prompt}, participantCount: {Count}",
            userId, imageContext, request.Prompt, request.ParticipantCount);

        // Generate image (returns base64, does NOT save to disk)
        var generationResult = await _imageService.GenerateImageAsync(
            request.Prompt,
            userId,
            imageContext,
            participantCount: request.ParticipantCount);

        if (generationResult.IsFailure)
        {
            return generationResult;
        }

        _logger.LogInformation("Image generated successfully for user {UserId} (base64)", userId);

        return generationResult;
    }
}
