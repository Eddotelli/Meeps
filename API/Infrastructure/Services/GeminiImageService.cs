using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Enums;
using API.Infrastructure.Configuration;

namespace API.Infrastructure.Services;

public class GeminiImageService : IImageGenerationService
{
    private readonly GoogleAISettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiImageService> _logger;

    public GeminiImageService(
        IOptions<GoogleAISettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<GeminiImageService> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com");
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        _logger = logger;
    }

    public async Task<Result<byte[]>> GenerateImageAsync(string userPrompt, ImageContext context, int? participantCount = null)
    {
        try
        {
            _logger.LogInformation("Generating image with Gemini API, context: {Context}, participantCount: {Count}", context, participantCount);

            if (string.IsNullOrEmpty(_settings.ApiKey))
            {
                _logger.LogError("Gemini API key is not configured");
                return Result.Failure<byte[]>(ImageErrors.GenerationFailed);
            }

            // Build full prompt with context
            var fullPrompt = BuildPrompt(userPrompt, context, participantCount);

            // Build request payload
            var requestPayload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = fullPrompt }
                        }
                    }
                },
                generationConfig = new
                {
                    imageConfig = new
                    {
                        aspectRatio = context == ImageContext.Profile ? "1:1" : "16:9"
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload);
            _logger.LogDebug("Gemini API request payload size: {Size} bytes", jsonContent.Length);

            // Make API request
            var url = $"/v1beta/models/{_settings.Model}:generateContent?key={_settings.ApiKey}";
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini API request failed: {StatusCode}, Error: {Error}",
                    response.StatusCode, errorContent);
                return Result.Failure<byte[]>(ImageErrors.GenerationFailed);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Gemini API response size: {Size} bytes", responseContent.Length);

            // Parse response
            var jsonResponse = JsonDocument.Parse(responseContent);

            // Navigate: candidates[0].content.parts[].inlineData.data (camelCase!)
            if (jsonResponse.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var contentObj) &&
                    contentObj.TryGetProperty("parts", out var parts))
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        // Note: Gemini uses camelCase "inlineData", not snake_case "inline_data"
                        if (part.TryGetProperty("inlineData", out var inlineData) &&
                            inlineData.TryGetProperty("data", out var base64Data))
                        {
                            var base64String = base64Data.GetString();
                            if (!string.IsNullOrEmpty(base64String))
                            {
                                var imageBytes = Convert.FromBase64String(base64String);
                                _logger.LogInformation("Image generated successfully with Gemini API, size: {Size} bytes",
                                    imageBytes.Length);
                                return Result.Success(imageBytes);
                            }
                        }
                    }
                }
            }

            _logger.LogWarning("No image data found in Gemini API response");
            return Result.Failure<byte[]>(ImageErrors.GenerationFailed);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to Gemini API failed");
            return Result.Failure<byte[]>(ImageErrors.GenerationFailed);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini API response");
            return Result.Failure<byte[]>(ImageErrors.GenerationFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating image with Gemini API");
            return Result.Failure<byte[]>(ImageErrors.GenerationFailed);
        }
    }

    private string BuildPrompt(string userPrompt, ImageContext context, int? participantCount)
    {
        var basePrompt = context switch
        {
            ImageContext.Profile =>
                "Professional portrait photo, friendly and approachable expression, " +
                "well-lit with natural lighting, clean neutral background, " +
                "high quality, suitable for social media profile picture. ",

            ImageContext.Event =>
                BuildEventPrompt(participantCount),

            _ => string.Empty
        };

        return $"{basePrompt}User description: {userPrompt}. " +
               "Photorealistic style, high detail, professional quality, safe for work content only.";
    }

    private string BuildEventPrompt(int? participantCount)
    {
        var peopleDescription = participantCount.HasValue
            ? $"with exactly {participantCount.Value} people"
            : "with a diverse group of people";

        return $"Vibrant and engaging activity photo {peopleDescription}, energetic and inviting atmosphere, " +
               "clear scene with people enjoying themselves together, dynamic composition, " +
               "high quality, suitable for event promotion and social media. " +
               "CRITICAL: Ensure diverse and inclusive representation - people should represent " +
               "various ethnicities, skin tones (including Black, Brown, Asian, Middle Eastern, Indigenous, and other backgrounds), " +
               "ages, genders, and appearances equally. " +
               "ABSOLUTELY NO TEXT, NO LETTERS, NO WORDS, NO NUMBERS, NO CAPTIONS, NO SIGNS of any kind in the image. ";
    }
}
