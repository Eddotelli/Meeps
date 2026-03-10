namespace API.Infrastructure.Services;

/// <summary>
/// Service for encoding/decoding integer IDs to/from hash strings.
/// Used to prevent enumeration and guessing of sequential IDs in URLs.
/// </summary>
public interface IHashIdService
{
    /// <summary>
    /// Encodes an integer ID to a hash string.
    /// </summary>
    /// <param name="id">The integer ID to encode</param>
    /// <returns>Hash string representation of the ID</returns>
    string Encode(int id);

    /// <summary>
    /// Decodes a hash string back to an integer ID.
    /// </summary>
    /// <param name="hash">The hash string to decode</param>
    /// <returns>The integer ID, or null if invalid hash</returns>
    int? Decode(string hash);
}
