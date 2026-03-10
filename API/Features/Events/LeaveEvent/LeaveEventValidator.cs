using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.LeaveEvent;

public class LeaveEventValidator : AbstractValidator<LeaveEventRequest>
{
    public LeaveEventValidator()
    {
        RuleFor(x => x.EventId)
            .GreaterThan(0)
            .WithMessage("Event ID must be greater than 0");
    }
}
