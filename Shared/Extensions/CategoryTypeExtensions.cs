namespace Shared.Extensions;

using Shared.Enums;

/// <summary>
/// Extension methods for CategoryType enum to handle localization and common operations.
/// </summary>
public static class CategoryTypeExtensions
{
    /// <summary>
    /// Gets the localization key for the category type.
    /// This key should match the keys in localization/common.json files.
    /// </summary>
    public static string GetLocalizationKey(this CategoryType category)
    {
        return category switch
        {
            CategoryType.Sports => "categorySports",
            CategoryType.Music => "categoryMusic",
            CategoryType.Gaming => "categoryGaming",
            CategoryType.Food => "categoryFoodDrink",
            CategoryType.Arts => "categoryArtsCulture",
            CategoryType.Technology => "categoryTechnology",
            CategoryType.Outdoor => "categoryOutdoorAdventure",
            CategoryType.Social => "categorySocial",
            CategoryType.Education => "categoryEducation",
            CategoryType.Other => "categoryOther",
            _ => "categoryOther"
        };
    }

    /// <summary>
    /// Gets all category types as an enumerable.
    /// </summary>
    public static IEnumerable<CategoryType> GetAll()
    {
        return Enum.GetValues<CategoryType>();
    }
}
