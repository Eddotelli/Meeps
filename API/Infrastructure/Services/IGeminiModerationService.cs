using Shared.Common.Results;

namespace API.Infrastructure.Services;

public interface IGeminiModerationService
{
    Task<Result<ModerationResult>> ModerateMessageAsync(string message);
}

public class ModerationResult
{
    public bool IsInappropriate { get; set; }
    public int Severity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
