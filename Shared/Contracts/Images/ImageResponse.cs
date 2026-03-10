namespace Shared.Contracts.Images;

public class ImageResponse
{
    public string? ImageUrl { get; set; }
    public string? Base64Image { get; set; }
    public string? MimeType { get; set; } = "image/png";
    public string? MessageKey { get; set; }
}
