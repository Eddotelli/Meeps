using Shared.Common.Results;
using Shared.Enums;

namespace API.Infrastructure.Services;

public interface IImageStorageService
{
    /// <summary>
    /// Saves image data (from AI generation or upload) to disk
    /// </summary>
    Task<Result<string>> SaveImageAsync(
        byte[] imageData,
        int userId,
        ImageContext context,
        int? eventId = null,
        string? fileName = null);

    /// <summary>
    /// Saves an uploaded file to disk
    /// </summary>
    Task<Result<string>> SaveUploadedImageAsync(
        IFormFile file,
        int userId,
        ImageContext context,
        int? eventId = null);

    /// <summary>
    /// Deletes an image from disk
    /// </summary>
    Task<Result> DeleteImageAsync(string imageUrl);

    /// <summary>
    /// Converts file path to URL
    /// </summary>
    string GetImageUrl(string filePath);

    /// <summary>
    /// Saves a base64-encoded image to disk
    /// </summary>
    Task<Result<string>> SaveBase64ImageAsync(
        string base64Image,
        int userId,
        ImageContext context,
        int? eventId = null);
}
