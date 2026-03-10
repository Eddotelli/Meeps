namespace Shared.Contracts.Events;

/// <summary>
/// Represents a localized warning about event edit constraints
/// </summary>
public class ConstraintWarning
{
    /// <summary>
    /// Localization key for the warning message
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Parameters to be used in the localized message (e.g., age ranges, participant counts)
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}
