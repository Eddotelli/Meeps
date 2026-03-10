using Shared.Enums;
using Shared.Extensions;
using MudBlazor;

namespace Client.Helpers;

/// <summary>
/// Provides utility methods for rendering user profile badges consistently across the application
/// </summary>
public static class ProfileBadgeHelper
{
    /// <summary>
    /// Badge information for rendering
    /// </summary>
    public class BadgeInfo
    {
        public string Icon { get; set; } = string.Empty;
        public string TooltipKey { get; set; } = string.Empty;
        public string? CustomTooltip { get; set; }
        public string CssClass { get; set; } = string.Empty;
        public string? DisplayText { get; set; }
        public BadgeType Type { get; set; }
    }

    public enum BadgeType
    {
        Verified,
        Gender,
        Age,
        Blocked
    }

    /// <summary>
    /// Gets all applicable badges for a user profile
    /// </summary>
    /// <param name="isVerified">Whether the user is verified</param>
    /// <param name="gender">The user's gender</param>
    /// <param name="birthDate">The user's birth date</param>
    /// <param name="isBlocked">Whether the user is blocked</param>
    /// <param name="showBlocked">Whether to include the blocked badge (e.g., false for own profile)</param>
    /// <returns>List of badge information to render</returns>
    public static List<BadgeInfo> GetProfileBadges(
        bool isVerified,
        Gender? gender,
        DateTime? birthDate,
        bool isBlocked = false,
        bool showBlocked = true)
    {
        var badges = new List<BadgeInfo>();

        if (isVerified)
        {
            badges.Add(new BadgeInfo
            {
                Icon = Icons.Material.Filled.Verified,
                TooltipKey = "verified",
                CssClass = "verified-badge",
                Type = BadgeType.Verified
            });
        }

        if (gender.HasValue)
        {
            badges.Add(new BadgeInfo
            {
                Icon = GetGenderIcon(gender.Value),
                TooltipKey = gender.Value.GetLocalizationKey(),
                CssClass = "gender-badge",
                Type = BadgeType.Gender
            });
        }

        if (birthDate.HasValue)
        {
            var age = CalculateAge(birthDate.Value);
            if (age > 0)
            {
                badges.Add(new BadgeInfo
                {
                    Icon = string.Empty, // Age badge uses text, not icon
                    TooltipKey = "yearsOld",
                    CustomTooltip = age.ToString(),
                    CssClass = "age-badge",
                    DisplayText = age.ToString(),
                    Type = BadgeType.Age
                });
            }
        }

        if (isBlocked && showBlocked)
        {
            badges.Add(new BadgeInfo
            {
                Icon = Icons.Material.Filled.Block,
                TooltipKey = "blocked",
                CssClass = "blocked-badge",
                Type = BadgeType.Blocked
            });
        }

        return badges;
    }

    /// <summary>
    /// Gets the Material Design icon for a gender
    /// </summary>
    public static string GetGenderIcon(Gender gender)
    {
        return gender switch
        {
            Gender.Male => Icons.Material.Filled.Male,
            Gender.Female => Icons.Material.Filled.Female,
            Gender.NonBinary => Icons.Material.Filled.Transgender,
            Gender.Other => Icons.Material.Filled.Transgender,
            Gender.PreferNotToSay => Icons.Material.Filled.Person,
            _ => Icons.Material.Filled.Person
        };
    }

    /// <summary>
    /// Calculates age from birth date
    /// </summary>
    public static int CalculateAge(DateTime birthDate)
    {
        var age = DateTime.Today.Year - birthDate.Year;
        if (birthDate.Date > DateTime.Today.AddYears(-age))
            age--;
        return age;
    }

    /// <summary>
    /// Gets formatted age text with unit
    /// </summary>
    /// <param name="birthDate">The user's birth date</param>
    /// <param name="yearsText">Localized text for "years" (e.g., from I18n.GetCommon("years"))</param>
    /// <returns>Formatted age string (e.g., "25 years") or "-" if no birth date</returns>
    public static string GetAgeText(DateTime? birthDate, string yearsText)
    {
        if (!birthDate.HasValue)
            return "-";

        var age = CalculateAge(birthDate.Value);
        return $"{age} {yearsText}";
    }
}
