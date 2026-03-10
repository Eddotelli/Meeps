using FluentValidation;
using Shared.Contracts.Images;
using Shared.Enums;

namespace API.Features.Images.UploadImage;

public class UploadImageValidator : AbstractValidator<IFormFile>
{
    public UploadImageValidator()
    {
        RuleFor(file => file)
            .NotNull()
            .WithMessage("No file provided");

        RuleFor(file => file.Length)
            .GreaterThan(0)
            .WithMessage("File is empty")
            .LessThanOrEqualTo(5 * 1024 * 1024)
            .WithMessage("File size must not exceed 5MB");

        RuleFor(file => file.FileName)
            .Must(HasValidExtension)
            .WithMessage("Invalid file format. Only JPG, PNG, and WebP are allowed");
    }

    private bool HasValidExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".webp";
    }
}
