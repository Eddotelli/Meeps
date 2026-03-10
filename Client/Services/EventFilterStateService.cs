using Blazored.LocalStorage;
using Shared.Enums;
using Client.Common;

namespace Client.Services;

public interface IEventFilterStateService
{
    Task InitializeAsync();
    Task<string> GetSearchQueryAsync();
    Task SetSearchQueryAsync(string value);
    Task<string> GetSelectedSortByAsync();
    Task SetSelectedSortByAsync(string value);
    Task<bool> GetShowOnlyMyEventsAsync();
    Task SetShowOnlyMyEventsAsync(bool value);
    Task<bool> GetShowOnlyVerifiedEventsAsync();
    Task SetShowOnlyVerifiedEventsAsync(bool value);
    Task<HashSet<CategoryType>> GetSelectedCategoriesAsync();
    Task SetSelectedCategoriesAsync(HashSet<CategoryType> value);
    Task<GenderRestriction?> GetSelectedGenderAsync();
    Task SetSelectedGenderAsync(GenderRestriction? value);
    Task<int> GetMinAgeValueAsync();
    Task SetMinAgeValueAsync(int value);
    Task<int> GetMaxAgeValueAsync();
    Task SetMaxAgeValueAsync(int value);
    Task ResetFiltersAsync();
    Task ClearStateAsync();
    Task<EventFilterParams> GetCurrentFiltersAsync();
}

public class EventFilterStateService : IEventFilterStateService
{
    private const string SearchQueryKey = "EventFilter_SearchQuery";
    private const string SortByKey = "EventFilter_SortBy";
    private const string ShowOnlyMyEventsKey = "EventFilter_ShowOnlyMyEvents";
    private const string ShowOnlyVerifiedEventsKey = "EventFilter_ShowOnlyVerifiedEvents";
    private const string SelectedCategoriesKey = "EventFilter_SelectedCategories";
    private const string SelectedGenderKey = "EventFilter_SelectedGender";
    private const string MinAgeKey = "EventFilter_MinAge";
    private const string MaxAgeKey = "EventFilter_MaxAge";

    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<EventFilterStateService> _logger;
    private bool _initialized = false;

    // In-memory cache för att undvika för många LocalStorage calls
    private string _searchQuery = string.Empty;
    private string _selectedSortBy = "distance";
    private bool _showOnlyMyEvents = false;
    private bool _showOnlyVerifiedEvents = false;
    private HashSet<CategoryType> _selectedCategories = new();
    private GenderRestriction? _selectedGender = null;
    private int _minAgeValue = 18;
    private int _maxAgeValue = 99;

    public EventFilterStateService(ILocalStorageService localStorage, ILogger<EventFilterStateService> logger)
    {
        _localStorage = localStorage;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _logger.LogInformation("Initializing EventFilterStateService from LocalStorage");

        _searchQuery = await _localStorage.GetItemAsync<string>(SearchQueryKey) ?? string.Empty;
        _selectedSortBy = await _localStorage.GetItemAsync<string>(SortByKey) ?? "distance";
        _showOnlyMyEvents = await _localStorage.GetItemAsync<bool>(ShowOnlyMyEventsKey);
        _showOnlyVerifiedEvents = await _localStorage.GetItemAsync<bool>(ShowOnlyVerifiedEventsKey);

        var categories = await _localStorage.GetItemAsync<List<CategoryType>>(SelectedCategoriesKey);
        _selectedCategories = categories != null ? new HashSet<CategoryType>(categories) : new();

        _selectedGender = await _localStorage.GetItemAsync<GenderRestriction?>(SelectedGenderKey);
        _minAgeValue = await _localStorage.GetItemAsync<int>(MinAgeKey);
        if (_minAgeValue == 0) _minAgeValue = 18; // Default if not set
        _maxAgeValue = await _localStorage.GetItemAsync<int>(MaxAgeKey);
        if (_maxAgeValue == 0) _maxAgeValue = 99; // Default if not set

        _initialized = true;
        _logger.LogDebug("EventFilterStateService initialized with {CategoryCount} categories", _selectedCategories.Count);
    }

    public Task<string> GetSearchQueryAsync() => Task.FromResult(_searchQuery);
    public async Task SetSearchQueryAsync(string value)
    {
        _searchQuery = value;
        await _localStorage.SetItemAsync(SearchQueryKey, value);
        _logger.LogDebug("Search query updated: {SearchQuery}", value);
    }

    public Task<string> GetSelectedSortByAsync() => Task.FromResult(_selectedSortBy);
    public async Task SetSelectedSortByAsync(string value)
    {
        _selectedSortBy = value;
        await _localStorage.SetItemAsync(SortByKey, value);
        _logger.LogDebug("Sort by updated: {SortBy}", value);
    }

    public Task<bool> GetShowOnlyMyEventsAsync() => Task.FromResult(_showOnlyMyEvents);
    public async Task SetShowOnlyMyEventsAsync(bool value)
    {
        _showOnlyMyEvents = value;
        await _localStorage.SetItemAsync(ShowOnlyMyEventsKey, value);
        _logger.LogDebug("Show only my events updated: {ShowOnlyMyEvents}", value);
    }

    public Task<bool> GetShowOnlyVerifiedEventsAsync() => Task.FromResult(_showOnlyVerifiedEvents);
    public async Task SetShowOnlyVerifiedEventsAsync(bool value)
    {
        _showOnlyVerifiedEvents = value;
        await _localStorage.SetItemAsync(ShowOnlyVerifiedEventsKey, value);
        _logger.LogDebug("Show only verified events updated: {ShowOnlyVerifiedEvents}", value);
    }

    public Task<HashSet<CategoryType>> GetSelectedCategoriesAsync() => Task.FromResult(_selectedCategories);
    public async Task SetSelectedCategoriesAsync(HashSet<CategoryType> value)
    {
        _selectedCategories = value;
        // Convert to List for LocalStorage serialization
        await _localStorage.SetItemAsync(SelectedCategoriesKey, value.ToList());
        _logger.LogDebug("Selected categories updated: {Count} categories", value.Count);
    }

    public Task<GenderRestriction?> GetSelectedGenderAsync() => Task.FromResult(_selectedGender);
    public async Task SetSelectedGenderAsync(GenderRestriction? value)
    {
        _selectedGender = value;
        await _localStorage.SetItemAsync(SelectedGenderKey, value);
        _logger.LogDebug("Selected gender updated: {Gender}", value);
    }

    public Task<int> GetMinAgeValueAsync() => Task.FromResult(_minAgeValue);
    public async Task SetMinAgeValueAsync(int value)
    {
        _minAgeValue = value;
        await _localStorage.SetItemAsync(MinAgeKey, value);
        _logger.LogDebug("Min age updated: {MinAge}", value);
    }

    public Task<int> GetMaxAgeValueAsync() => Task.FromResult(_maxAgeValue);
    public async Task SetMaxAgeValueAsync(int value)
    {
        _maxAgeValue = value;
        await _localStorage.SetItemAsync(MaxAgeKey, value);
        _logger.LogDebug("Max age updated: {MaxAge}", value);
    }

    public async Task ResetFiltersAsync()
    {
        _logger.LogInformation("Resetting all filter values");

        _searchQuery = string.Empty;
        _selectedSortBy = "distance";
        _showOnlyMyEvents = false;
        _showOnlyVerifiedEvents = false;
        _selectedCategories.Clear();
        _selectedGender = null;
        _minAgeValue = 18;
        _maxAgeValue = 99;

        await _localStorage.RemoveItemAsync(SearchQueryKey);
        await _localStorage.RemoveItemAsync(SortByKey);
        await _localStorage.RemoveItemAsync(ShowOnlyMyEventsKey);
        await _localStorage.RemoveItemAsync(ShowOnlyVerifiedEventsKey);
        await _localStorage.RemoveItemAsync(SelectedCategoriesKey);
        await _localStorage.RemoveItemAsync(SelectedGenderKey);
        await _localStorage.RemoveItemAsync(MinAgeKey);
        await _localStorage.RemoveItemAsync(MaxAgeKey);
    }

    public async Task ClearStateAsync()
    {
        _logger.LogInformation("Clearing all filter state and localStorage on logout");

        // Reset in-memory state to defaults
        _searchQuery = string.Empty;
        _selectedSortBy = "distance";
        _showOnlyMyEvents = false;
        _showOnlyVerifiedEvents = false;
        _selectedCategories.Clear();
        _selectedGender = null;
        _minAgeValue = 18;
        _maxAgeValue = 99;

        // Reset initialized flag to force re-initialization on next login
        _initialized = false;

        // Clear all localStorage (not just filter keys)
        await _localStorage.ClearAsync();
    }

    public async Task<EventFilterParams> GetCurrentFiltersAsync()
    {
        await InitializeAsync();
        return new EventFilterParams
        {
            SearchQuery = _searchQuery,
            SortBy = _selectedSortBy,
            ShowOnlyMyEvents = _showOnlyMyEvents,
            ShowOnlyVerifiedEvents = _showOnlyVerifiedEvents,
            CategoryFilters = _selectedCategories.ToList(),
            GenderFilter = _selectedGender,
            MinAge = _minAgeValue,
            MaxAge = _maxAgeValue
        };
    }
}
