using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.JoinEvent;

public class JoinEventValidator : AbstractValidator<JoinEventRequest>
{
    public JoinEventValidator()
    {
        RuleFor(x => x.EventId)
            .GreaterThan(0)
            .WithMessage("Event ID must be greater than 0");
    }
}
