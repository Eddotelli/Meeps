namespace Client.Helpers;

/// <summary>
/// Provides utility methods for user display formatting
/// </summary>
public static class UserHelper
{
    /// <summary>
    /// Gets the initials from a user's display name or username.
    /// Returns the first letter of the first two words if available,
    /// otherwise the first two characters of the name.
    /// </summary>
    /// <param name="name">The user's display name or username</param>
    /// <returns>Uppercase initials (1-2 characters) or "?" if name is empty</returns>
    public static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var trimmedName = name.Trim();
        var parts = trimmedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();

        return trimmedName.Substring(0, Math.Min(2, trimmedName.Length)).ToUpper();
    }
}
