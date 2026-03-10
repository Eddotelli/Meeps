using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.UnblockParticipant;

public class UnblockParticipantValidator : AbstractValidator<UnblockParticipantRequest>
{
    public UnblockParticipantValidator()
    {
        RuleFor(x => x.EventId)
            .GreaterThan(0)
            .WithMessage("Event ID must be greater than 0");

        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage("User ID must be greater than 0");
    }
}
