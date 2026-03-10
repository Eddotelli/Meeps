using FluentValidation;
using Shared.Contracts.Users;

namespace API.Features.Users.DeleteAccount;

public class DeleteAccountValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required");

        RuleFor(x => x.ConfirmUnderstanding)
            .Equal(true)
            .WithMessage("You must confirm that you understand what will be deleted");
    }
}
