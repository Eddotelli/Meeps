namespace Client.Services;

/// <summary>
/// Service for managing event filter state and notifying components of changes
/// </summary>
public interface IEventFilterService
{
    /// <summary>
    /// Event triggered when location or search radius changes
    /// </summary>
    event Action? OnFilterChanged;

    /// <summary>
    /// Event triggered when only radius changes (for live filtering without API call)
    /// </summary>
    event Action<int>? OnRadiusChanged;

    /// <summary>
    /// Notify all subscribers that filters have changed
    /// </summary>
    void NotifyFilterChanged();

    /// <summary>
    /// Notify all subscribers that radius has changed
    /// </summary>
    /// <param name="radiusKm">New radius in kilometers</param>
    void NotifyRadiusChanged(int radiusKm);
}
