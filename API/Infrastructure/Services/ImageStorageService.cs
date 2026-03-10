using Microsoft.Extensions.Options;
using Shared.Common.Errors;
using Shared.Common.Results;
using Shared.Enums;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using API.Infrastructure.Configuration;

namespace API.Infrastructure.Services;

public class ImageStorageService : IImageStorageService
{
    private readonly ImageSettings _imageSettings;
    private readonly ILogger<ImageStorageService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public ImageStorageService(
        IOptions<ImageSettings> imageSettings,
        ILogger<ImageStorageService> logger,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _imageSettings = imageSettings.Value;
        _logger = logger;
        _environment = environment;
        _configuration = configuration;
    }

    public async Task<Result<string>> SaveImageAsync(
        byte[] imageData,
        int userId,
        ImageContext context,
        int? eventId = null,
        string? fileName = null)
    {
        try
        {
            // Generate filename if not provided
            fileName ??= $"{Guid.NewGuid()}_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg";

            // Get directory path
            var directory = GetDirectoryPath(userId, context, eventId);
            Directory.CreateDirectory(directory);

            // For Profile context: delete all existing images (user can only have one profile image)
            // For Event context with eventId: delete all existing images (event can only have one image)
            if (context == ImageContext.Profile || (context == ImageContext.Event && eventId.HasValue))
            {
                DeleteAllFilesInDirectory(directory);
            }

            // Full file path
            var filePath = Path.Combine(directory, fileName);

            // Process and save image
            using var image = Image.Load(imageData);
            await ResizeAndSaveImageAsync(image, filePath, context);

            // Return URL
            var url = GetImageUrl(filePath);
            _logger.LogInformation("Image saved successfully: {Url}", url);
            return Result.Success(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save image for user {UserId}, context {Context}", userId, context);
            return Result.Failure<string>(ImageErrors.UploadFailed);
        }
    }

    public async Task<Result<string>> SaveUploadedImageAsync(
        IFormFile file,
        int userId,
        ImageContext context,
        int? eventId = null)
    {
        try
        {
            // Validate file
            var validationResult = ValidateFile(file);
            if (validationResult.IsFailure)
            {
                return Result.Failure<string>(validationResult.Error!);
            }

            // Read file data
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var imageData = memoryStream.ToArray();

            // Generate filename with original extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";

            // Save using common method
            return await SaveImageAsync(imageData, userId, context, eventId, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save uploaded image for user {UserId}", userId);
            return Result.Failure<string>(ImageErrors.UploadFailed);
        }
    }

    public async Task<Result<string>> SaveBase64ImageAsync(
        string base64Image,
        int userId,
        ImageContext context,
        int? eventId = null)
    {
        try
        {
            // Remove data URL prefix if present (e.g., "data:image/png;base64,")
            var base64Data = base64Image.Contains(",")
                ? base64Image.Split(',')[1]
                : base64Image;

            // Convert base64 to bytes
            var imageBytes = Convert.FromBase64String(base64Data);

            _logger.LogInformation("Converting base64 to image, size: {Size} bytes", imageBytes.Length);

            // Generate filename
            var fileName = $"{Guid.NewGuid()}_{DateTime.UtcNow:yyyyMMddHHmmss}.png";

            // Save using existing SaveImageAsync method
            return await SaveImageAsync(imageBytes, userId, context, eventId, fileName);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Invalid base64 format");
            return Result.Failure<string>(ImageErrors.InvalidFormat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save base64 image for user {UserId}", userId);
            return Result.Failure<string>(ImageErrors.UploadFailed);
        }
    }

    public async Task<Result> DeleteImageAsync(string imageUrl)
    {
        try
        {
            // Convert URL to file path
            var filePath = GetFilePathFromUrl(imageUrl);
            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
                _logger.LogInformation("Deleted image: {FilePath}", filePath);
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete image: {ImageUrl}", imageUrl);
            return Result.Failure(ImageErrors.UploadFailed);
        }
    }

    public string GetImageUrl(string filePath)
    {
        string relativeUrl;

        if (_imageSettings.UseAzureFileShare)
        {
            // In production with Azure File Share: convert /mounts/uploads/... to /uploads/...
            var mountPath = _imageSettings.AzureFileSharePath;
            relativeUrl = filePath.Replace(mountPath, "/uploads").Replace("\\", "/");
        }
        else
        {
            // In development: convert wwwroot path to relative URL
            var webRootPath = _environment.WebRootPath;
            var relativePath = filePath.Replace(webRootPath, "").Replace("\\", "/");
            relativeUrl = relativePath.StartsWith("/") ? relativePath : $"/{relativePath}";
        }

        // Get base URL from configuration
        var baseUrl = _configuration["AppUrl"] ?? string.Empty;

        // Return full URL (e.g., https://localhost:7000/uploads/users/1/profile/image.jpg)
        return $"{baseUrl.TrimEnd('/')}{relativeUrl}";
    }

    private Result ValidateFile(IFormFile file)
    {
        // Check if file exists
        if (file == null || file.Length == 0)
        {
            return Result.Failure(ImageErrors.NoFile);
        }

        // Check file size
        var maxSizeBytes = _imageSettings.MaxFileSizeMB * 1024 * 1024;
        if (file.Length > maxSizeBytes)
        {
            return Result.Failure(ImageErrors.FileTooLarge);
        }

        // Check file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant().TrimStart('.');
        if (!_imageSettings.AllowedFormats.Contains(extension))
        {
            return Result.Failure(ImageErrors.InvalidFormat);
        }

        // Verify it's actually an image (magic bytes check)
        try
        {
            using var stream = file.OpenReadStream();
            var image = Image.Identify(stream);
            if (image == null)
            {
                return Result.Failure(ImageErrors.InvalidFormat);
            }
        }
        catch
        {
            return Result.Failure(ImageErrors.InvalidFormat);
        }

        return Result.Success();
    }

    private async Task ResizeAndSaveImageAsync(Image image, string filePath, ImageContext context)
    {
        var (width, height) = context switch
        {
            ImageContext.Profile => (_imageSettings.ProfileImageWidth, _imageSettings.ProfileImageHeight),
            ImageContext.Event => (_imageSettings.EventImageWidth, _imageSettings.EventImageHeight),
            _ => (1200, 675)
        };

        // Resize image
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(width, height),
            Mode = ResizeMode.Crop
        }));

        // Determine format and save
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        switch (extension)
        {
            case ".jpg":
            case ".jpeg":
                await image.SaveAsJpegAsync(filePath, new JpegEncoder { Quality = 85 });
                break;
            case ".png":
                await image.SaveAsPngAsync(filePath, new PngEncoder());
                break;
            case ".webp":
                await image.SaveAsWebpAsync(filePath, new WebpEncoder { Quality = 85 });
                break;
            default:
                await image.SaveAsJpegAsync(filePath, new JpegEncoder { Quality = 85 });
                break;
        }
    }

    private string GetDirectoryPath(int userId, ImageContext context, int? eventId = null)
    {
        // Use Azure File Share mount in production, wwwroot in development
        var rootPath = _imageSettings.UseAzureFileShare
            ? _imageSettings.AzureFileSharePath
            : Path.Combine(_environment.WebRootPath, "uploads");

        var basePath = Path.Combine(rootPath, "users", userId.ToString());
        return context switch
        {
            ImageContext.Profile => Path.Combine(basePath, "profile"),
            ImageContext.Event when eventId.HasValue => Path.Combine(basePath, "events", eventId.Value.ToString()),
            ImageContext.Event => Path.Combine(basePath, "events", "temp"),
            _ => basePath
        };
    }

    private string GetFilePathFromUrl(string url)
    {
        var webRootPath = _environment.WebRootPath;
        var relativePath = url.TrimStart('/').Replace("/", "\\");
        return Path.Combine(webRootPath, relativePath);
    }

    private void DeleteAllFilesInDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                var files = Directory.GetFiles(directory);
                foreach (var file in files)
                {
                    File.Delete(file);
                    _logger.LogInformation("Deleted old image: {FilePath}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete old images in directory: {Directory}", directory);
        }
    }
}
