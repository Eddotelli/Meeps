namespace Shared.Extensions;

using Shared.Enums;

/// <summary>
/// Extension methods for Gender enum to handle localization and common operations.
/// </summary>
public static class GenderExtensions
{
    /// <summary>
    /// Gets the localization key for the gender type.
    /// This key should match the keys in localization/common.json files.
    /// </summary>
    public static string GetLocalizationKey(this Gender gender)
    {
        return gender switch
        {
            Gender.Male => "male",
            Gender.Female => "female",
            Gender.NonBinary => "nonBinary",
            Gender.Other => "other",
            Gender.PreferNotToSay => "preferNotToSay",
            _ => "other"
        };
    }

    /// <summary>
    /// Gets all gender types as an enumerable.
    /// </summary>
    public static IEnumerable<Gender> GetAll()
    {
        return Enum.GetValues<Gender>();
    }

    /// <summary>
    /// Maps GenderRestriction to Gender for eligibility checks.
    /// Returns true if the user's gender matches the event's restriction.
    /// </summary>
    public static bool MatchesRestriction(this Gender? userGender, GenderRestriction restriction)
    {
        if (restriction == GenderRestriction.None)
            return true;

        if (!userGender.HasValue)
            return false;

        return (restriction, userGender.Value) switch
        {
            (GenderRestriction.MaleOnly, Gender.Male) => true,
            (GenderRestriction.FemaleOnly, Gender.Female) => true,
            (GenderRestriction.NonBinaryOnly, Gender.NonBinary) => true,
            _ => false
        };
    }
}
