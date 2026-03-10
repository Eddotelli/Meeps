using System.ComponentModel.DataAnnotations;

namespace Shared.Common.Validation;

/// <summary>
/// Validates that the birth year indicates the person is at least the specified age.
/// </summary>
public class MinimumAgeAttribute : ValidationAttribute
{
    private readonly int _minimumAge;

    public MinimumAgeAttribute(int minimumAge)
    {
        _minimumAge = minimumAge;
        ErrorMessage = $"You must be at least {_minimumAge} years old";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        if (value is not int birthYear)
        {
            return new ValidationResult("Birth year must be a valid number");
        }

        var currentYear = DateTime.Now.Year;
        var maxValidBirthYear = currentYear - _minimumAge;

        // Check if birth year is in valid range
        if (birthYear < 1900)
        {
            return new ValidationResult("Birth year cannot be before 1900");
        }

        if (birthYear > maxValidBirthYear)
        {
            return new ValidationResult(ErrorMessage);
        }

        return ValidationResult.Success;
    }
}
