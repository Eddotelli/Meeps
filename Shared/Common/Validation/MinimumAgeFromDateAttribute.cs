using System.ComponentModel.DataAnnotations;

namespace Shared.Common.Validation;

/// <summary>
/// Validates that the birth date indicates the person is at least the specified age.
/// </summary>
public class MinimumAgeFromDateAttribute : ValidationAttribute
{
    private readonly int _minimumAge;

    public MinimumAgeFromDateAttribute(int minimumAge)
    {
        _minimumAge = minimumAge;
        // ErrorMessage will be localized by LocalizedDataAnnotationsValidator
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If null, it's optional - let Required handle this
        if (value == null)
        {
            return ValidationResult.Success;
        }

        if (value is not DateTime birthDate)
        {
            return new ValidationResult("Birth date must be a valid date");
        }

        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;

        // Adjust if birthday hasn't occurred yet this year
        if (birthDate.Date > today.AddYears(-age))
        {
            age--;
        }

        if (age < _minimumAge)
        {
            return new ValidationResult($"You must be at least {_minimumAge} years old");
        }

        return ValidationResult.Success;
    }
}
