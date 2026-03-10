using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.BlockParticipant;

public class BlockParticipantValidator : AbstractValidator<BlockParticipantRequest>
{
    public BlockParticipantValidator()
    {
        RuleFor(x => x.EventId)
            .GreaterThan(0)
            .WithMessage("Event ID must be greater than 0");

        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage("User ID must be greater than 0");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Reason))
            .WithMessage("Reason cannot exceed 500 characters");
    }
}
