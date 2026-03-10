namespace API.Infrastructure.Configuration;

public class ImageSettings
{
    public int MaxFileSizeMB { get; set; } = 5;
    public string[] AllowedFormats { get; set; } = ["jpg", "jpeg", "png", "webp"];
    public string StoragePath { get; set; } = "wwwroot/uploads";
    public bool UseAzureFileShare { get; set; } = false;
    public string AzureFileSharePath { get; set; } = "/mounts/uploads";
    public int ProfileImageWidth { get; set; } = 500;
    public int ProfileImageHeight { get; set; } = 500;
    public int EventImageWidth { get; set; } = 1200;
    public int EventImageHeight { get; set; } = 675;
}
