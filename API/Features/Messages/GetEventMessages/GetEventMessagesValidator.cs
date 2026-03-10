using FluentValidation;
using Shared.Contracts.Messages;

namespace API.Features.Messages.GetEventMessages;

public class GetEventMessagesValidator : AbstractValidator<GetEventMessagesRequest>
{
    public GetEventMessagesValidator()
    {
        RuleFor(x => x.EventHash)
            .NotEmpty()
            .WithMessage("Event hash must be provided");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100");
    }
}
