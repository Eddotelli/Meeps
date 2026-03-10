using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Client.Services;

public class I18nService : II18nService
{
    private readonly HttpClient _http;
    private readonly ILogger<I18nService> _logger;
    private Dictionary<string, string> _errors = new();
    private Dictionary<string, string> _common = new();
    private Dictionary<string, string> _validation = new();
    private Dictionary<string, string> _messages = new();
    private Dictionary<string, string> _helptext = new();
    private string _currentCulture = "sv-SE";

    public event Action? OnLanguageChanged;

    public I18nService(HttpClient http, ILogger<I18nService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _errors = await _http.GetFromJsonAsync<Dictionary<string, string>>
                ($"localization/{_currentCulture}/errors.json") ?? new();
            _common = await _http.GetFromJsonAsync<Dictionary<string, string>>
                ($"localization/{_currentCulture}/common.json") ?? new();
            _validation = await _http.GetFromJsonAsync<Dictionary<string, string>>
                ($"localization/{_currentCulture}/validation.json") ?? new();
            _messages = await _http.GetFromJsonAsync<Dictionary<string, string>>
                ($"localization/{_currentCulture}/messages.json") ?? new();
            _helptext = await _http.GetFromJsonAsync<Dictionary<string, string>>
                ($"localization/{_currentCulture}/helptext.json") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load localization files");
        }
    }

    public async Task SetCultureAsync(string culture)
    {
        _currentCulture = culture;
        await InitializeAsync();
        OnLanguageChanged?.Invoke();
    }

    public string GetError(string key) => _errors.GetValueOrDefault(key, key);
    public string GetCommon(string key) => _common.GetValueOrDefault(key, key);
    public string GetValidation(string key) => _validation.GetValueOrDefault(key, key);
    public string GetMessage(string key) => _messages.GetValueOrDefault(key, key);
    public string GetHelpText(string key) => _helptext.GetValueOrDefault(key, key);

    public string CurrentCulture => _currentCulture;
}
