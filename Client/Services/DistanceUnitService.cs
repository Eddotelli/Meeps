namespace Client.Services;

public class DistanceUnitService : IDistanceUnitService
{
    private const double KM_TO_MILES = 0.621371;
    private const double MILES_TO_KM = 1.60934;

    private readonly II18nService _i18n;
    private bool _useImperial = false;

    public string DistanceUnit => _useImperial ? "mi" : "km";
    public int MinRadius => _useImperial ? 3 : 5;  // ~3 miles = 5 km
    public int MaxRadius => _useImperial ? 31 : 50;  // ~31 miles = 50 km

    public event Action? OnUnitChanged;

    public DistanceUnitService(II18nService i18n)
    {
        _i18n = i18n;

        // Set initial unit based on current culture
        UpdateUnitBasedOnCulture();

        // Listen for language changes
        _i18n.OnLanguageChanged += UpdateUnitBasedOnCulture;
    }

    private void UpdateUnitBasedOnCulture()
    {
        // en-US uses imperial (miles), sv-SE uses metric (km)
        bool shouldUseImperial = _i18n.CurrentCulture == "en-US";

        if (_useImperial != shouldUseImperial)
        {
            _useImperial = shouldUseImperial;
            OnUnitChanged?.Invoke();
        }
    }

    public double GetDisplayRadius(int radiusKm)
    {
        return _useImperial ? radiusKm * KM_TO_MILES : radiusKm;
    }

    public int ConvertToKilometers(int displayValue)
    {
        return _useImperial ? (int)Math.Round(displayValue * MILES_TO_KM) : displayValue;
    }

    public void SetDistanceUnit(bool useImperial)
    {
        if (_useImperial != useImperial)
        {
            _useImperial = useImperial;
            OnUnitChanged?.Invoke();
        }
    }
}
