namespace Shared.Common.Results;

public record Error(
    string Code,
    string Message,
    int StatusCode = 400,
    string? Type = null,
    string? Detail = null)
{
    public static readonly Error None = new(string.Empty, string.Empty, 200);

    // Helper to generate Type URI from error code
    public string GetTypeUri() => Type ?? $"https://api.meeps.com/errors/{Code.ToLower().Replace('.', '/')}";
}
