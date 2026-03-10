using Shared.Common.Results;
using Shared.Contracts.Images;
using Shared.Enums;

namespace API.Infrastructure.Services;

public interface IImageService
{
    /// <summary>
    /// Uploads and saves an image file
    /// </summary>
    Task<Result<ImageResponse>> UploadImageAsync(IFormFile file, int userId, ImageContext context, int? eventId = null);

    /// <summary>
    /// Generates an image using AI and saves it
    /// </summary>
    Task<Result<ImageResponse>> GenerateImageAsync(string prompt, int userId, ImageContext context, int? eventId = null, int? participantCount = null);
}
