using Shared.Common.Constants;
using Shared.Common.Results;
using Shared.Contracts.Images;
using Shared.Enums;

namespace API.Infrastructure.Services;

public class ImageService : IImageService
{
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IImageStorageService _imageStorageService;
    private readonly ILogger<ImageService> _logger;

    public ImageService(
        IImageGenerationService imageGenerationService,
        IImageStorageService imageStorageService,
        ILogger<ImageService> logger)
    {
        _imageGenerationService = imageGenerationService;
        _imageStorageService = imageStorageService;
        _logger = logger;
    }

    public async Task<Result<ImageResponse>> UploadImageAsync(
        IFormFile file,
        int userId,
        ImageContext context,
        int? eventId = null)
    {
        _logger.LogInformation("Uploading image for user {UserId}, context: {Context}", userId, context);

        var result = await _imageStorageService.SaveUploadedImageAsync(file, userId, context, eventId);

        if (result.IsFailure)
        {
            return Result.Failure<ImageResponse>(result.Error!);
        }

        return Result.Success(new ImageResponse
        {
            ImageUrl = result.Value,
            MessageKey = MessageKeys.ImageUploaded
        });
    }

    public async Task<Result<ImageResponse>> GenerateImageAsync(
        string prompt,
        int userId,
        ImageContext context,
        int? eventId = null,
        int? participantCount = null)
    {
        _logger.LogInformation("Generating image for user {UserId}, context: {Context}, participantCount: {Count}", userId, context, participantCount);

        // Generate image with AI (returns base64, does NOT save to disk)
        var generationResult = await _imageGenerationService.GenerateImageAsync(prompt, context, participantCount);
        if (generationResult.IsFailure)
        {
            return Result.Failure<ImageResponse>(generationResult.Error!);
        }

        // Convert bytes to base64 string
        var base64String = Convert.ToBase64String(generationResult.Value);

        _logger.LogInformation("Image generated successfully, size: {Size} bytes", generationResult.Value.Length);

        return Result.Success(new ImageResponse
        {
            Base64Image = base64String,
            MimeType = "image/png",
            MessageKey = MessageKeys.ImageGenerated
        });
    }
}
