using Shared.Common.Results;
using Shared.Contracts.Images;

namespace Client.Services.ApiClients;

/// <summary>
/// Interface for images API client.
/// Handles all image-related API calls (upload and generation).
/// </summary>
public interface IImagesApiClient
{
    /// <summary>
    /// Uploads an image file
    /// </summary>
    Task<Result<ImageResponse>> UploadImageAsync(Stream imageStream, string fileName, string context);

    /// <summary>
    /// Generates an image using AI
    /// </summary>
    Task<Result<ImageResponse>> GenerateImageAsync(string prompt, string context, int? participantCount = null);
}
