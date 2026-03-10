using FluentValidation;
using Shared.Contracts.Images;

namespace API.Features.Images.GenerateImage;

public class GenerateImageValidator : AbstractValidator<GenerateImageRequest>
{
    public GenerateImageValidator()
    {
        RuleFor(x => x.Prompt)
            .NotEmpty()
            .WithMessage("Prompt is required")
            .MinimumLength(10)
            .WithMessage("Prompt must be at least 10 characters")
            .MaximumLength(500)
            .WithMessage("Prompt must not exceed 500 characters");

        RuleFor(x => x.Context)
            .NotEmpty()
            .WithMessage("Context is required")
            .Must(BeValidContext)
            .WithMessage("Context must be either 'Profile' or 'Event'");

        RuleFor(x => x.ParticipantCount)
            .InclusiveBetween(1, 20)
            .When(x => x.ParticipantCount.HasValue)
            .WithMessage("Participant count must be between 1 and 20");
    }

    private bool BeValidContext(string context)
    {
        return context is "Profile" or "Event";
    }
}
