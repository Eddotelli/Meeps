using MudBlazor;
using Shared.Enums;

namespace Client.Services;

/// <summary>
/// Provides icon and color mappings for event categories
/// </summary>
public static class CategoryIconHelper
{
    /// <summary>
    /// Gets the appropriate Material icon for a given category type
    /// </summary>
    /// <param name="category">The category type</param>
    /// <returns>The Material icon string for the category</returns>
    public static string GetIconForCategory(CategoryType category)
    {
        return category switch
        {
            CategoryType.Sports => Icons.Material.Filled.SportsSoccer,
            CategoryType.Music => Icons.Material.Filled.MusicNote,
            CategoryType.Gaming => Icons.Material.Filled.SportsEsports,
            CategoryType.Food => Icons.Material.Filled.Restaurant,
            CategoryType.Arts => Icons.Material.Filled.Palette,
            CategoryType.Technology => Icons.Material.Filled.Computer,
            CategoryType.Outdoor => Icons.Material.Filled.Park,
            CategoryType.Social => Icons.Material.Filled.Groups,
            CategoryType.Education => Icons.Material.Filled.School,
            CategoryType.Other => Icons.Material.Filled.MoreHoriz,
            _ => Icons.Material.Filled.Category
        };
    }

    /// <summary>
    /// Gets the appropriate color for a given category type
    /// </summary>
    /// <param name="category">The category type</param>
    /// <returns>The MudBlazor color for the category</returns>
    public static Color GetColorForCategory(CategoryType category)
    {
        return category switch
        {
            CategoryType.Sports => Color.Success,
            CategoryType.Music => Color.Secondary,
            CategoryType.Gaming => Color.Info,
            CategoryType.Food => Color.Warning,
            CategoryType.Arts => Color.Tertiary,
            CategoryType.Technology => Color.Primary,
            CategoryType.Outdoor => Color.Success,
            CategoryType.Social => Color.Info,
            CategoryType.Education => Color.Primary,
            CategoryType.Other => Color.Default,
            _ => Color.Default
        };
    }
}
