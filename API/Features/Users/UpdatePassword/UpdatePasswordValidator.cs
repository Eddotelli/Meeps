using FluentValidation;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdatePassword;

public class UpdatePasswordValidator : AbstractValidator<UpdatePasswordRequest>
{
    public UpdatePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters")
            .MaximumLength(100)
            .WithMessage("Password cannot exceed 100 characters");

        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty()
            .WithMessage("Please confirm your new password")
            .Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match");
    }
}
