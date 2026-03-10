namespace Client.Services;

public interface IDistanceUnitService
{
    /// <summary>
    /// Current distance unit (e.g., "km" or "mi")
    /// </summary>
    string DistanceUnit { get; }

    /// <summary>
    /// Minimum radius value in current unit
    /// </summary>
    int MinRadius { get; }

    /// <summary>
    /// Maximum radius value in current unit
    /// </summary>
    int MaxRadius { get; }

    /// <summary>
    /// Event triggered when distance unit changes
    /// </summary>
    event Action? OnUnitChanged;

    /// <summary>
    /// Convert kilometers to display radius in current unit
    /// </summary>
    double GetDisplayRadius(int radiusKm);

    /// <summary>
    /// Convert display radius to kilometers
    /// </summary>
    int ConvertToKilometers(int displayValue);

    /// <summary>
    /// Set distance unit preference
    /// </summary>
    void SetDistanceUnit(bool useImperial);
}
