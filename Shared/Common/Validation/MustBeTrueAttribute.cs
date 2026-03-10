using System.ComponentModel.DataAnnotations;

namespace Shared.Common.Validation;

/// <summary>
/// Validates that a boolean property is true.
/// Useful for terms acceptance checkboxes.
/// </summary>
public class MustBeTrueAttribute : ValidationAttribute
{
    public MustBeTrueAttribute()
    {
        // ErrorMessage will be localized by LocalizedDataAnnotationsValidator
    }

    public override bool IsValid(object? value)
    {
        if (value is bool boolValue)
        {
            return boolValue;
        }
        return false;
    }
}
