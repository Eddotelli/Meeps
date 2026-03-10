# Implementera LocalStorage för EventFilter State

## Problem

EventFilter-komponenten förlorar sina filter-värden när:

1. Location eller radius ändras (component re-render)
2. Användaren navigerar till andra sidor och kommer tillbaka
3. Sidan laddas om

Vi behöver behålla filter-värdena permanent tills användaren själv väljer att rensa dem.

## Lösning

Använd LocalStorage med Scoped Service för att persistera filter-state mellan sessions och navigering.

---

## Steg 1: Installera Blazored.LocalStorage

Om inte redan installerat:

```bash
cd Client
dotnet add package Blazored.LocalStorage
```

Verifiera att paketet finns i `Client/Client.csproj`.

---

## Steg 2: Registrera LocalStorage Service

I `Client/Program.cs`, lägg till efter befintliga service-registreringar:

```csharp
builder.Services.AddBlazoredLocalStorage();
```

---

## Steg 3: Skapa EventFilterStateService

Skapa ny fil: `Client/Services/EventFilterStateService.cs`

```csharp
using Blazored.LocalStorage;
using Shared.Enums;

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
    Task<HashSet<CategoryType>> GetSelectedCategoriesAsync();
    Task SetSelectedCategoriesAsync(HashSet<CategoryType> value);
    Task<GenderRestriction?> GetSelectedGenderAsync();
    Task SetSelectedGenderAsync(GenderRestriction? value);
    Task<int> GetMinAgeValueAsync();
    Task SetMinAgeValueAsync(int value);
    Task<int> GetMaxAgeValueAsync();
    Task SetMaxAgeValueAsync(int value);
    Task ResetFiltersAsync();
    Task<EventFilterParams> GetCurrentFiltersAsync();
}

public class EventFilterStateService : IEventFilterStateService
{
    private const string SearchQueryKey = "EventFilter_SearchQuery";
    private const string SortByKey = "EventFilter_SortBy";
    private const string ShowOnlyMyEventsKey = "EventFilter_ShowOnlyMyEvents";
    private const string SelectedCategoriesKey = "EventFilter_SelectedCategories";
    private const string SelectedGenderKey = "EventFilter_SelectedGender";
    private const string MinAgeKey = "EventFilter_MinAge";
    private const string MaxAgeKey = "EventFilter_MaxAge";

    private readonly ILocalStorageService _localStorage;
    private bool _initialized = false;

    // In-memory cache för att undvika för många LocalStorage calls
    private string _searchQuery = string.Empty;
    private string _selectedSortBy = "distance";
    private bool _showOnlyMyEvents = false;
    private HashSet<CategoryType> _selectedCategories = new();
    private GenderRestriction? _selectedGender = null;
    private int _minAgeValue = 18;
    private int _maxAgeValue = 99;

    public EventFilterStateService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _searchQuery = await _localStorage.GetItemAsync<string>(SearchQueryKey) ?? string.Empty;
        _selectedSortBy = await _localStorage.GetItemAsync<string>(SortByKey) ?? "distance";
        _showOnlyMyEvents = await _localStorage.GetItemAsync<bool>(ShowOnlyMyEventsKey);
        _selectedCategories = await _localStorage.GetItemAsync<HashSet<CategoryType>>(SelectedCategoriesKey) ?? new();
        _selectedGender = await _localStorage.GetItemAsync<GenderRestriction?>(SelectedGenderKey);
        _minAgeValue = await _localStorage.GetItemAsync<int>(MinAgeKey);
        if (_minAgeValue == 0) _minAgeValue = 18; // Default if not set
        _maxAgeValue = await _localStorage.GetItemAsync<int>(MaxAgeKey);
        if (_maxAgeValue == 0) _maxAgeValue = 99; // Default if not set

        _initialized = true;
    }

    public Task<string> GetSearchQueryAsync() => Task.FromResult(_searchQuery);
    public async Task SetSearchQueryAsync(string value)
    {
        _searchQuery = value;
        await _localStorage.SetItemAsync(SearchQueryKey, value);
    }

    public Task<string> GetSelectedSortByAsync() => Task.FromResult(_selectedSortBy);
    public async Task SetSelectedSortByAsync(string value)
    {
        _selectedSortBy = value;
        await _localStorage.SetItemAsync(SortByKey, value);
    }

    public Task<bool> GetShowOnlyMyEventsAsync() => Task.FromResult(_showOnlyMyEvents);
    public async Task SetShowOnlyMyEventsAsync(bool value)
    {
        _showOnlyMyEvents = value;
        await _localStorage.SetItemAsync(ShowOnlyMyEventsKey, value);
    }

    public Task<HashSet<CategoryType>> GetSelectedCategoriesAsync() => Task.FromResult(_selectedCategories);
    public async Task SetSelectedCategoriesAsync(HashSet<CategoryType> value)
    {
        _selectedCategories = value;
        await _localStorage.SetItemAsync(SelectedCategoriesKey, value);
    }

    public Task<GenderRestriction?> GetSelectedGenderAsync() => Task.FromResult(_selectedGender);
    public async Task SetSelectedGenderAsync(GenderRestriction? value)
    {
        _selectedGender = value;
        await _localStorage.SetItemAsync(SelectedGenderKey, value);
    }

    public Task<int> GetMinAgeValueAsync() => Task.FromResult(_minAgeValue);
    public async Task SetMinAgeValueAsync(int value)
    {
        _minAgeValue = value;
        await _localStorage.SetItemAsync(MinAgeKey, value);
    }

    public Task<int> GetMaxAgeValueAsync() => Task.FromResult(_maxAgeValue);
    public async Task SetMaxAgeValueAsync(int value)
    {
        _maxAgeValue = value;
        await _localStorage.SetItemAsync(MaxAgeKey, value);
    }

    public async Task ResetFiltersAsync()
    {
        _searchQuery = string.Empty;
        _selectedSortBy = "distance";
        _showOnlyMyEvents = false;
        _selectedCategories.Clear();
        _selectedGender = null;
        _minAgeValue = 18;
        _maxAgeValue = 99;

        await _localStorage.RemoveItemAsync(SearchQueryKey);
        await _localStorage.RemoveItemAsync(SortByKey);
        await _localStorage.RemoveItemAsync(ShowOnlyMyEventsKey);
        await _localStorage.RemoveItemAsync(SelectedCategoriesKey);
        await _localStorage.RemoveItemAsync(SelectedGenderKey);
        await _localStorage.RemoveItemAsync(MinAgeKey);
        await _localStorage.RemoveItemAsync(MaxAgeKey);
    }

    public async Task<EventFilterParams> GetCurrentFiltersAsync()
    {
        await InitializeAsync();
        return new EventFilterParams
        {
            SearchQuery = _searchQuery,
            SortBy = _selectedSortBy,
            ShowOnlyMyEvents = _showOnlyMyEvents,
            CategoryFilters = _selectedCategories.ToList(),
            GenderFilter = _selectedGender,
            MinAge = _minAgeValue,
            MaxAge = _maxAgeValue
        };
    }
}
```

---

## Steg 4: Registrera EventFilterStateService

I `Client/Program.cs`, lägg till:

```csharp
builder.Services.AddScoped<IEventFilterStateService, EventFilterStateService>();
```

Lägg till denna rad tillsammans med andra service-registreringar (nära ApiClients, I18nService, etc.).

---

## Steg 5: Uppdatera EventFilter.razor

### 5.1: Lägg till injection

Överst i filen, lägg till:

```razor
@inject IEventFilterStateService FilterState
```

### 5.2: Ändra private fields till lokala variabler

Ersätt alla befintliga private fields (som `private string _searchQuery = string.Empty;`) med:

```csharp
// Lokala variabler för UI binding (synkas med FilterState service)
private string _searchQuery = string.Empty;
private string _selectedSortBy = "distance";
private bool _showOnlyMyEvents = false;
private HashSet<CategoryType> _selectedCategories = new();
private GenderRestriction? _selectedGender = null;
private int _minAgeValue = 18;
private int _maxAgeValue = 99;
```

### 5.3: Uppdatera OnInitializedAsync

Lägg till i början av metoden:

```csharp
protected override async Task OnInitializedAsync()
{
    await FilterState.InitializeAsync();

    // Ladda värden från LocalStorage
    _searchQuery = await FilterState.GetSearchQueryAsync();
    _selectedSortBy = await FilterState.GetSelectedSortByAsync();
    _showOnlyMyEvents = await FilterState.GetShowOnlyMyEventsAsync();
    _selectedCategories = await FilterState.GetSelectedCategoriesAsync();
    _selectedGender = await FilterState.GetSelectedGenderAsync();
    _minAgeValue = await FilterState.GetMinAgeValueAsync();
    _maxAgeValue = await FilterState.GetMaxAgeValueAsync();

    // Befintlig initialization code fortsätter här...
    I18n.OnLanguageChanged += StateHasChanged;
    // etc...
}
```

### 5.4: Uppdatera Properties med auto-save

Hitta befintliga properties och ersätt med:

```csharp
public string SearchQuery
{
    get => _searchQuery;
    set
    {
        if (_searchQuery != value)
        {
            _searchQuery = value;
            _ = FilterState.SetSearchQueryAsync(value);
        }
    }
}

public string SelectedSortBy
{
    get => _selectedSortBy;
    set
    {
        if (_selectedSortBy != value)
        {
            _selectedSortBy = value;
            _ = FilterState.SetSelectedSortByAsync(value);
        }
    }
}

public bool ShowOnlyMyEvents
{
    get => _showOnlyMyEvents;
    set
    {
        if (_showOnlyMyEvents != value)
        {
            _showOnlyMyEvents = value;
            _ = FilterState.SetShowOnlyMyEventsAsync(value);
        }
    }
}

public GenderRestriction? SelectedGender
{
    get => _selectedGender;
    set
    {
        if (_selectedGender != value)
        {
            _selectedGender = value;
            _ = FilterState.SetSelectedGenderAsync(value);
        }
    }
}

public int MinAgeValue
{
    get => _minAgeValue;
    set
    {
        if (_minAgeValue != value)
        {
            _minAgeValue = value;
            _ = FilterState.SetMinAgeValueAsync(value);
        }
    }
}

public int MaxAgeValue
{
    get => _maxAgeValue;
    set
    {
        if (_maxAgeValue != value)
        {
            _maxAgeValue = value;
            _ = FilterState.SetMaxAgeValueAsync(value);
        }
    }
}

public HashSet<CategoryType> SelectedCategories
{
    get => _selectedCategories;
    set => _selectedCategories = value;
}
```

### 5.5: Uppdatera OnCategoryToggle

```csharp
private async Task OnCategoryToggle(CategoryType category, bool isChecked)
{
    if (isChecked)
    {
        _selectedCategories.Add(category);
    }
    else
    {
        _selectedCategories.Remove(category);
    }
    await FilterState.SetSelectedCategoriesAsync(_selectedCategories);
    await OnFilterChanged();
}
```

### 5.6: Uppdatera ClearFilters

```csharp
private async Task ClearFilters()
{
    await FilterState.ResetFiltersAsync();

    // Uppdatera lokala värden
    _searchQuery = string.Empty;
    _selectedSortBy = "distance";
    _showOnlyMyEvents = false;
    _selectedCategories.Clear();
    _selectedGender = null;
    _minAgeValue = 18;
    _maxAgeValue = 99;

    await NotifyFilterChanged();
}
```

### 5.7: Uppdatera NotifyFilterChanged

```csharp
private async Task NotifyFilterChanged()
{
    var filterParams = await FilterState.GetCurrentFiltersAsync();
    await OnFilterChange.InvokeAsync(filterParams);
}
```

---

## Steg 6: Uppdatera Home.razor

### 6.1: Lägg till injection

```razor
@inject IEventFilterStateService FilterState
```

### 6.2: Uppdatera OnInitializedAsync

I `Home.razor`, lägg till efter `LoadEligibleEvents()`:

```csharp
protected override async Task OnInitializedAsync()
{
    var authState = await AuthStateProvider.GetAuthenticationStateAsync();
    var user = authState.User;

    _isAuthenticated = user.Identity?.IsAuthenticated ?? false;

    if (!_isAuthenticated)
    {
        Navigation.NavigateTo("/login");
        return;
    }

    I18n.OnLanguageChanged += StateHasChanged;
    EventFilterService.OnFilterChanged += HandleGlobalFilterChanged;
    EventFilterService.OnRadiusChanged += HandleRadiusChanged;

    await FilterState.InitializeAsync();

    await LoadUserRadius();
    await LoadEligibleEvents();

    // Applicera sparade filter efter events har laddats
    _currentFilters = await FilterState.GetCurrentFiltersAsync();
    ApplyFilters();
}
```

### 6.3: Ta bort @key attributet

I `Home.razor`, hitta EventFilter-komponenten och ta bort `@key="eventFilterKey"`:

```razor
<EventFilter OnFilterChange="HandleFilterChange" TotalCount="@_allEvents.Count"
    FilteredCount="@_filteredEvents.Count" />
```

Ta även bort konstanten `private const string eventFilterKey = "event-filter-component";` från @code-blocket.

---

## Steg 7: Testa Implementeringen

### Test 1: Filter persistens vid navigation

1. Gå till home page
2. Sätt några filter (kategori, sökord, etc.)
3. Navigera till Settings
4. Navigera tillbaka till home
5. **Förväntat resultat:** Filtren ska vara kvar

### Test 2: Filter persistens vid page reload

1. Gå till home page
2. Sätt några filter
3. Tryck F5 för att ladda om sidan
4. **Förväntat resultat:** Filtren ska vara kvar

### Test 3: Filter persistens vid location/radius ändring

1. Gå till home page
2. Sätt några filter
3. Ändra location eller radius i navbar
4. **Förväntat resultat:** Filtren ska vara kvar, men events ska uppdateras

### Test 4: Clear filters

1. Gå till home page med filter satta
2. Klicka "Clear Filters"
3. Ladda om sidan
4. **Förväntat resultat:** Inga filter ska vara satta

---

## Felsökning

### Problem: Filter syns inte efter reload

- Öppna browser DevTools → Application → Local Storage
- Kontrollera att `EventFilter_*` keys finns
- Kontrollera att `InitializeAsync()` anropas i både EventFilter och Home.razor

### Problem: Filter sparas inte

- Kontrollera att properties använder `await FilterState.Set...Async()` i setters
- Kontrollera att `Blazored.LocalStorage` är korrekt installerad och registrerad

### Problem: Categories sparas inte

- Kontrollera att `OnCategoryToggle` anropar `await FilterState.SetSelectedCategoriesAsync()`
- LocalStorage kan ha problem med HashSet - om så, ändra till List i servicen

---

## Fördelar med denna lösning

✅ Filter persisteras mellan sessions
✅ Filter persisteras vid navigation
✅ Filter påverkas inte av location/radius ändringar
✅ In-memory cache för bättre performance
✅ Enkel att testa (kan rensa LocalStorage via DevTools)
✅ Följer Blazor best practices

---

## Följ Meeps Code Standards

- ✅ Använd ILogger för logging (lägg till om behövs)
- ✅ Använd I18nService för alla texter
- ✅ Följ Result Pattern om service kan misslyckas
- ✅ Inga magic strings - använd konstanter för LocalStorage keys
- ✅ Alla metoder ska vara async Task när de använder await

---

**Slutfört!** Nu ska filtren sparas permanent i LocalStorage och behållas mellan sessions, navigation och location-ändringar.
