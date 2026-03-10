using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shared.Common.Results;
using Shared.Contracts.Images;
using Shared.Common.Errors;
using Client.Common;

namespace Client.Services.ApiClients;

public class ImagesApiClient : IImagesApiClient
{
    private readonly ApiClient _apiClient;
    private readonly HttpClient _httpClient;

    public ImagesApiClient(ApiClient apiClient, HttpClient httpClient)
    {
        _apiClient = apiClient;
        _httpClient = httpClient;
    }

    public async Task<Result<ImageResponse>> UploadImageAsync(
        Stream imageStream,
        string fileName,
        string context)
    {
        try
        {
            using var content = new MultipartFormDataContent();

            var streamContent = new StreamContent(imageStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync(
                $"/api/images/upload?context={context}",
                content);

            if (!response.IsSuccessStatusCode)
            {
                var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
                var errorCode = problemDetails?.ErrorCode ?? CommonErrors.ServerError.Code;
                var message = problemDetails?.Detail ?? "Failed to upload image";
                return Result.Failure<ImageResponse>(new Error(errorCode, message));
            }

            var result = await response.Content.ReadFromJsonAsync<ImageResponse>();
            return result != null
                ? Result.Success(result)
                : Result.Failure<ImageResponse>(new Error("IMAGE.UPLOAD_FAILED", "Failed to parse response"));
        }
        catch (Exception)
        {
            return Result.Failure<ImageResponse>(
                new Error("IMAGE.UPLOAD_FAILED", "Failed to upload image"));
        }
    }

    public async Task<Result<ImageResponse>> GenerateImageAsync(string prompt, string context, int? participantCount = null)
    {
        var request = new GenerateImageRequest
        {
            Prompt = prompt,
            Context = context,
            ParticipantCount = participantCount
        };

        return await _apiClient.PostAsync<ImageResponse>(
            "/api/images/generate",
            request);
    }
}
