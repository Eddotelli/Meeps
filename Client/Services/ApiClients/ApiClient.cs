using System.Net.Http.Json;
using System.Text.Json;
using Shared.Common.Results;
using Shared.Common.Errors;
using Client.Common;

namespace Client.Services.ApiClients;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ValidationErrorMapper _errorMapper;

    public ApiClient(HttpClient httpClient, ValidationErrorMapper errorMapper)
    {
        _httpClient = httpClient;
        _errorMapper = errorMapper;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<Result<TResponse>> PostAsync<TResponse>(string url, object request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
                return data != null
                    ? Result.Success(data)
                    : Result.Failure<TResponse>(CommonErrors.Client.NullResponse);
            }

            return await ParseErrorResponse<TResponse>(response);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<TResponse>(new Error(CommonErrors.Client.NetworkError.Code, $"Network error: {ex.Message}", 0));
        }
        catch (Exception ex)
        {
            return Result.Failure<TResponse>(new Error(CommonErrors.Client.UnknownError.Code, $"An unexpected error occurred: {ex.Message}", 0));
        }
    }

    public async Task<Result> PostAsync(string url, object request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request);

            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);

            // Handle validation errors
            if (problemDetails?.Errors != null && problemDetails.Errors.Any())
            {
                var validationErrors = _errorMapper.MapValidationErrors(problemDetails.Errors);
                var errorMessage = string.Join("; ", validationErrors.SelectMany(kvp => kvp.Value));

                return Result.Failure(new Error(
                    problemDetails.ErrorCode ?? CommonErrors.Validation.ValidationError.Code,
                    errorMessage,
                    (int)response.StatusCode
                ));
            }

            var errorCode = problemDetails?.ErrorCode ?? ExtractErrorCodeFromType(problemDetails?.Type);

            return Result.Failure(new Error(
                errorCode,
                problemDetails?.Detail ?? problemDetails?.Title ?? string.Empty,
                (int)response.StatusCode
            ));
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure(new Error(CommonErrors.Client.NetworkError.Code, $"Network error: {ex.Message}", 0));
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error(CommonErrors.Client.UnknownError.Code, $"An unexpected error occurred: {ex.Message}", 0));
        }
    }

    public async Task<Result<TResponse>> GetAsync<TResponse>(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
                return data != null
                    ? Result.Success(data)
                    : Result.Failure<TResponse>(CommonErrors.Client.NullResponse);
            }

            return await ParseErrorResponse<TResponse>(response);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<TResponse>(new Error(CommonErrors.Client.NetworkError.Code, $"Network error: {ex.Message}", 0));
        }
        catch (Exception ex)
        {
            return Result.Failure<TResponse>(new Error(CommonErrors.Client.UnknownError.Code, $"An unexpected error occurred: {ex.Message}", 0));
        }
    }

    public async Task<Result<TResponse>> PutAsync<TResponse>(string url, object request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(url, request);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
                return data != null
                    ? Result.Success(data)
                    : Result.Failure<TResponse>(CommonErrors.Client.NullResponse);
            }

            return await ParseErrorResponse<TResponse>(response);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<TResponse>(new Error(CommonErrors.Client.NetworkError.Code, $"Network error: {ex.Message}", 0));
        }
        catch (Exception ex)
        {
            return Result.Failure<TResponse>(new Error(CommonErrors.Client.UnknownError.Code, $"An unexpected error occurred: {ex.Message}", 0));
        }
    }

    public async Task<Result> PutAsync(string url, object request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(url, request);

            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);

            // Handle validation errors
            if (problemDetails?.Errors != null && problemDetails.Errors.Any())
            {
                var validationErrors = _errorMapper.MapValidationErrors(problemDetails.Errors);
                var errorMessage = string.Join("; ", validationErrors.SelectMany(kvp => kvp.Value));

                return Result.Failure(new Error(
                    problemDetails.ErrorCode ?? CommonErrors.Validation.ValidationError.Code,
                    errorMessage,
                    (int)response.StatusCode
                ));
            }

            var errorCode = problemDetails?.ErrorCode ?? ExtractErrorCodeFromType(problemDetails?.Type);

            return Result.Failure(new Error(
                errorCode,
                problemDetails?.Detail ?? problemDetails?.Title ?? string.Empty,
                (int)response.StatusCode
            ));
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure(new Error(CommonErrors.Client.NetworkError.Code, $"Network error: {ex.Message}", 0));
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error(CommonErrors.Client.UnknownError.Code, $"An unexpected error occurred: {ex.Message}", 0));
        }
    }

    public async Task<Result> DeleteAsync(string url)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(url);

            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);

            // Handle validation errors
            if (problemDetails?.Errors != null && problemDetails.Errors.Any())
            {
                var validationErrors = _errorMapper.MapValidationErrors(problemDetails.Errors);
                var errorMessage = string.Join("; ", validationErrors.SelectMany(kvp => kvp.Value));

                return Result.Failure(new Error(
                    problemDetails.ErrorCode ?? CommonErrors.Validation.ValidationError.Code,
                    errorMessage,
                    (int)response.StatusCode
                ));
            }

            var errorCode = problemDetails?.ErrorCode ?? ExtractErrorCodeFromType(problemDetails?.Type);

            return Result.Failure(new Error(
                errorCode,
                problemDetails?.Detail ?? problemDetails?.Title ?? string.Empty,
                (int)response.StatusCode
            ));
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure(new Error(CommonErrors.Client.NetworkError.Code, $"Network error: {ex.Message}", 0));
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error(CommonErrors.Client.UnknownError.Code, $"An unexpected error occurred: {ex.Message}", 0));
        }
    }

    public async Task<Result<TResponse>> DeleteAsync<TResponse>(string url)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
                return data != null
                    ? Result.Success(data)
                    : Result.Failure<TResponse>(CommonErrors.Client.NullResponse);
            }

            return await ParseErrorResponse<TResponse>(response);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<TResponse>(new Error(CommonErrors.Client.NetworkError.Code, $"Network error: {ex.Message}", 0));
        }
        catch (Exception ex)
        {
            return Result.Failure<TResponse>(new Error(CommonErrors.Client.UnknownError.Code, $"An unexpected error occurred: {ex.Message}", 0));
        }
    }

    private async Task<Result<TResponse>> ParseErrorResponse<TResponse>(HttpResponseMessage response)
    {
        try
        {
            var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);

            // Handle validation errors specially
            if (problemDetails?.Errors != null && problemDetails.Errors.Any())
            {
                var validationErrors = _errorMapper.MapValidationErrors(problemDetails.Errors);
                var errorMessage = string.Join("; ", validationErrors.SelectMany(kvp => kvp.Value));

                return Result.Failure<TResponse>(new Error(
                    problemDetails.ErrorCode ?? CommonErrors.Validation.ValidationError.Code,
                    errorMessage,
                    (int)response.StatusCode
                ));
            }

            var errorCode = problemDetails?.ErrorCode ?? ExtractErrorCodeFromType(problemDetails?.Type);

            return Result.Failure<TResponse>(new Error(
                errorCode,
                problemDetails?.Detail ?? problemDetails?.Title ?? string.Empty,
                (int)response.StatusCode
            ));
        }
        catch
        {
            return Result.Failure<TResponse>(new Error(
                CommonErrors.ServerError.Code,
                CommonErrors.ServerError.Message,
                (int)response.StatusCode
            ));
        }
    }

    private string ExtractErrorCodeFromType(string? type)
    {
        if (string.IsNullOrEmpty(type))
            return CommonErrors.ServerError.Code;

        // Extract error code from URI like "https://api.meeps.com/errors/user/invalid-credentials"
        // Convert to "USER.INVALID_CREDENTIALS"
        var parts = type.Split('/');
        if (parts.Length >= 2)
        {
            var relevantParts = parts[^2..]; // Last 2 parts
            return string.Join('.', relevantParts.Select(p => p.Replace('-', '_').ToUpperInvariant()));
        }

        return CommonErrors.ServerError.Code;
    }
}
