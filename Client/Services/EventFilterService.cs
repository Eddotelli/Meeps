namespace Client.Services;

/// <summary>
/// Service for managing event filter state and notifying components of changes.
/// Used to coordinate live updates between navbar location changes and event displays.
/// </summary>
public class EventFilterService : IEventFilterService
{
    /// <inheritdoc />
    public event Action? OnFilterChanged;

    /// <inheritdoc />
    public event Action<int>? OnRadiusChanged;

    /// <inheritdoc />
    public void NotifyFilterChanged()
    {
        OnFilterChanged?.Invoke();
    }

    /// <inheritdoc />
    public void NotifyRadiusChanged(int radiusKm)
    {
        OnRadiusChanged?.Invoke(radiusKm);
    }
}
