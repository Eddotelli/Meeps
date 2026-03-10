namespace Shared.Enums;

/// <summary>
/// Represents gender identity options for users.
/// Stored as integers in the database for type-safety and consistency.
/// </summary>
public enum Gender
{
    Male = 1,
    Female = 2,
    NonBinary = 3,
    Other = 4,
    PreferNotToSay = 5
}
