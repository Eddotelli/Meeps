using HashidsNet;

namespace API.Infrastructure.Services;

/// <summary>
/// Implementation of HashId service using Hashids.net library.
/// Encodes/decodes integer IDs to/from short hash strings.
/// </summary>
public class HashIdService : IHashIdService
{
    private readonly Hashids _hashids;
    private readonly ILogger<HashIdService> _logger;

    public HashIdService(IConfiguration configuration, ILogger<HashIdService> logger)
    {
        _logger = logger;

        var salt = configuration["HashIds:Salt"];
        if (string.IsNullOrEmpty(salt))
        {
            throw new InvalidOperationException(
                "HashIds:Salt configuration is missing. Please set it in user secrets or appsettings.");
        }

        if (salt.Length < 16)
        {
            throw new InvalidOperationException(
                "HashIds:Salt must be at least 16 characters long for security.");
        }

        // Create Hashids instance with salt and minimum hash length of 6 characters
        _hashids = new Hashids(salt, minHashLength: 6);

        _logger.LogInformation("HashIdService initialized with minimum hash length: 6");
    }

    public string Encode(int id)
    {
        if (id <= 0)
        {
            throw new ArgumentException("ID must be greater than 0", nameof(id));
        }

        var hash = _hashids.Encode(id);
        _logger.LogDebug("Encoded ID {Id} to hash {Hash}", id, hash);
        return hash;
    }

    public int? Decode(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            _logger.LogWarning("Attempted to decode null or empty hash");
            return null;
        }

        try
        {
            var ids = _hashids.Decode(hash);

            if (ids.Length == 0)
            {
                _logger.LogWarning("Failed to decode hash: {Hash}", hash);
                return null;
            }

            var id = ids[0];
            _logger.LogDebug("Decoded hash {Hash} to ID {Id}", hash, id);
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decoding hash: {Hash}", hash);
            return null;
        }
    }
}
