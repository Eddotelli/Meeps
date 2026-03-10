namespace Client.Services;

/// <summary>
/// Interface for localization service.
/// Provides access to translated strings from JSON files.
/// </summary>
public interface II18nService
{
    /// <summary>
    /// Initializes the service by loading all localization files for the current culture.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Changes the current culture and reloads all localization files.
    /// </summary>
    /// <param name="culture">Culture code (e.g., "en-US", "sv-SE")</param>
    Task SetCultureAsync(string culture);

    /// <summary>
    /// Gets an error message by key from errors.json.
    /// </summary>
    /// <param name="key">Translation key</param>
    /// <returns>Translated error message or the key if not found</returns>
    string GetError(string key);

    /// <summary>
    /// Gets a common string by key from common.json.
    /// </summary>
    /// <param name="key">Translation key</param>
    /// <returns>Translated common string or the key if not found</returns>
    string GetCommon(string key);

    /// <summary>
    /// Gets a validation message by key from validation.json.
    /// </summary>
    /// <param name="key">Translation key</param>
    /// <returns>Translated validation message or the key if not found</returns>
    string GetValidation(string key);

    /// <summary>
    /// Gets a message by key from messages.json.
    /// </summary>
    /// <param name="key">Translation key</param>
    /// <returns>Translated message or the key if not found</returns>
    string GetMessage(string key);

    /// <summary>
    /// Gets a help text by key from helptext.json.
    /// </summary>
    /// <param name="key">Translation key</param>
    /// <returns>Translated help text or the key if not found</returns>
    string GetHelpText(string key);

    /// <summary>
    /// Gets the current culture code.
    /// </summary>
    string CurrentCulture { get; }

    /// <summary>
    /// Event raised when the language is changed.
    /// </summary>
    event Action? OnLanguageChanged;
}
