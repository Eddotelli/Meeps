namespace Shared.Extensions;

using Shared.Enums;

/// <summary>
/// Extension methods for GenderRestriction enum to handle localization and common operations.
/// </summary>
public static class GenderRestrictionExtensions
{
    /// <summary>
    /// Gets the localization key for the gender restriction type.
    /// This key should match the keys in localization/common.json files.
    /// </summary>
    public static string GetLocalizationKey(this GenderRestriction restriction)
    {
        return restriction switch
        {
            GenderRestriction.None => "noRestriction",
            GenderRestriction.MaleOnly => "maleOnly",
            GenderRestriction.FemaleOnly => "femaleOnly",
            GenderRestriction.NonBinaryOnly => "nonBinaryOnly",
            _ => "noRestriction"
        };
    }

    /// <summary>
    /// Gets all gender restriction types as an enumerable.
    /// </summary>
    public static IEnumerable<GenderRestriction> GetAll()
    {
        return Enum.GetValues<GenderRestriction>();
    }

    /// <summary>
    /// Gets available gender restrictions based on the user's gender.
    /// Users can only create events for their own gender group or for everyone (None).
    /// </summary>
    public static IEnumerable<GenderRestriction> GetAvailableRestrictions(Gender? userGender)
    {
        // If user has no gender or prefers not to say, they can only create events for everyone
        if (!userGender.HasValue ||
            userGender == Gender.Other ||
            userGender == Gender.PreferNotToSay)
        {
            return new[] { GenderRestriction.None };
        }

        // User can create events for their own gender or for everyone
        return userGender switch
        {
            Gender.Male => new[] { GenderRestriction.None, GenderRestriction.MaleOnly },
            Gender.Female => new[] { GenderRestriction.None, GenderRestriction.FemaleOnly },
            Gender.NonBinary => new[] { GenderRestriction.None, GenderRestriction.NonBinaryOnly },
            _ => new[] { GenderRestriction.None }
        };
    }

    /// <summary>
    /// Validates if a user with the given gender can set the specified restriction.
    /// </summary>
    public static bool CanUserSetRestriction(Gender? userGender, GenderRestriction restriction)
    {
        return GetAvailableRestrictions(userGender).Contains(restriction);
    }
}
