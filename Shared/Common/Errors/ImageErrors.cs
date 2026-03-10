using Shared.Common.Results;

namespace Shared.Common.Errors;

public static class ImageErrors
{
    public static readonly Error InvalidFormat = new(
        "IMAGE.INVALID_FORMAT",
        "Invalid image format. Use JPG, PNG or WebP",
        400);

    public static readonly Error FileTooLarge = new(
        "IMAGE.FILE_TOO_LARGE",
        "Image too large. Maximum 5MB",
        400);

    public static readonly Error GenerationFailed = new(
        "IMAGE.GENERATION_FAILED",
        "Failed to generate image",
        500);

    public static readonly Error UploadFailed = new(
        "IMAGE.UPLOAD_FAILED",
        "Failed to upload image",
        500);

    public static readonly Error InappropriateContent = new(
        "IMAGE.INAPPROPRIATE_CONTENT",
        "Content not allowed",
        400);

    public static readonly Error InvalidContext = new(
        "IMAGE.INVALID_CONTEXT",
        "Invalid image context. Must be Profile or Event",
        400);

    public static readonly Error NoFile = new(
        "IMAGE.NO_FILE",
        "No file provided",
        400);
}
