using Client.Common;

namespace Client.Services;

public class ValidationErrorMapper
{
    private readonly II18nService _i18n;

    public ValidationErrorMapper(II18nService i18n)
    {
        _i18n = i18n;
    }

    public string MapValidationError(ValidationError error)
    {
        if (error.Message == null || error.Field == null)
            return _i18n.GetValidation("fieldRequired");

        // Check if this is a custom error code (e.g., USER.GENDER_CONFLICT_WITH_EVENTS)
        if (!string.IsNullOrEmpty(error.Code) && error.Code.Contains('.'))
        {
            var errorMessage = _i18n.GetError(error.Code);
            // If we found a localized error message, use it
            if (errorMessage != error.Code)
                return errorMessage;
        }

        // Try to map based on field name and error code
        var localizedKey = GetLocalizationKey(error.Field, error.Code);
        var message = _i18n.GetValidation(localizedKey);

        // If key not found, return the original message or a generic one
        if (message == localizedKey)
        {
            // Try generic error based on code
            return TryMapByErrorCode(error.Code) ?? error.Message;
        }

        return message;
    }

    public Dictionary<string, List<string>> MapValidationErrors(List<ValidationError>? errors)
    {
        var result = new Dictionary<string, List<string>>();

        if (errors == null || !errors.Any())
            return result;

        foreach (var error in errors)
        {
            if (error.Field == null)
                continue;

            if (!result.ContainsKey(error.Field))
                result[error.Field] = new List<string>();

            result[error.Field].Add(MapValidationError(error));
        }

        return result;
    }

    private string GetLocalizationKey(string fieldName, string? errorCode)
    {
        // Convert field name to camelCase
        var camelField = ToCamelCase(fieldName);

        // Map based on error code patterns from FluentValidation
        return errorCode?.ToLowerInvariant() switch
        {
            "notemptyvalidator" or "notnullvalidator" => $"{camelField}Required",
            "emailvalidator" => "emailAddress",
            "maxlengthlengthvalidator" => $"{camelField}MaxLength",
            "minlengthlengthvalidator" => $"{camelField}MinLength",
            "lengthvalidator" => $"{camelField}Length",
            "rangevalidator" => $"{camelField}Range",
            "equalvalidator" => "passwordsDoNotMatch",
            _ => camelField
        };
    }

    private string? TryMapByErrorCode(string? errorCode)
    {
        if (string.IsNullOrEmpty(errorCode))
            return null;

        return errorCode.ToLowerInvariant() switch
        {
            "notemptyvalidator" or "notnullvalidator" => _i18n.GetValidation("required"),
            "emailvalidator" => _i18n.GetValidation("emailAddress"),
            "maxlengthlengthvalidator" => _i18n.GetValidation("maxLength"),
            "minlengthlengthvalidator" => _i18n.GetValidation("minLength"),
            "lengthvalidator" => _i18n.GetValidation("stringLength"),
            "rangevalidator" => _i18n.GetValidation("range"),
            "equalvalidator" => _i18n.GetValidation("compare"),
            _ => null
        };
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
            return value;

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
