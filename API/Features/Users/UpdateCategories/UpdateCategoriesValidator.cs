using FluentValidation;
using Shared.Contracts.Users;

namespace API.Features.Users.UpdateCategories;

public class UpdateCategoriesValidator : AbstractValidator<UpdateCategoriesRequest>
{
    public UpdateCategoriesValidator()
    {
        // Categories can be empty array
    }
}
