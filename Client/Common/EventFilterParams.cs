using Shared.Enums;

namespace Client.Common;

/// <summary>
/// Parameters for filtering events in the UI
/// </summary>
public class EventFilterParams
{
    public string SearchQuery { get; set; } = string.Empty;
    public string SortBy { get; set; } = "distance";
    public bool ShowOnlyMyEvents { get; set; }
    public bool ShowOnlyVerifiedEvents { get; set; }
    public List<CategoryType> CategoryFilters { get; set; } = new();
    public GenderRestriction? GenderFilter { get; set; }
    public int MinAge { get; set; } = 18;
    public int MaxAge { get; set; } = 100;
}
