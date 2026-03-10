using FluentValidation;
using Shared.Contracts.Events;

namespace API.Features.Events.DeleteEvent;

public class DeleteEventValidator : AbstractValidator<DeleteEventRequest>
{
    public DeleteEventValidator()
    {
        RuleFor(x => x.EventId)
            .GreaterThan(0)
            .WithMessage("EventId must be greater than 0");
    }
}
