using MudBlazor;

namespace Client.Services;

public static class AppTheme
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#58908f",
            PrimaryDarken = "#487170",
            PrimaryLighten = "#6aa8a6",
            PrimaryContrastText = "#ffffff"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#58908f",
            PrimaryDarken = "#487170",
            PrimaryLighten = "#6aa8a6",
            PrimaryContrastText = "#ffffff"
        }
    };
}
