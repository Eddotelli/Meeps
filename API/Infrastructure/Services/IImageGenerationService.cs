using Shared.Common.Results;
using Shared.Enums;

namespace API.Infrastructure.Services;

public interface IImageGenerationService
{
    /// <summary>
    /// Generates an image using Google Imagen based on user prompt and context
    /// </summary>
    Task<Result<byte[]>> GenerateImageAsync(string userPrompt, ImageContext context, int? participantCount = null);
}
