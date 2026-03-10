using FluentValidation;
using Shared.Contracts.Messages;

namespace API.Features.Messages.SendMessage;

public class SendMessageValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.EventId)
            .NotNull()
            .GreaterThan(0)
            .WithMessage("Event ID must be provided");

        RuleFor(x => x.Text)
            .NotNull()
            .WithMessage("Message cannot be null")
            .MaximumLength(1000)
            .WithMessage("Message must not exceed 1000 characters")
            .Must(text => text != null && text.Trim().Length > 0)
            .WithMessage("Message cannot be empty or contain only whitespace");
    }
}
