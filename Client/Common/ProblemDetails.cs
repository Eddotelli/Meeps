namespace Client.Common;

public record ProblemDetails
{
    public string? Type { get; init; }
    public string? Title { get; init; }
    public int? Status { get; init; }
    public string? Detail { get; init; }
    public string? Instance { get; init; }
    public string? ErrorCode { get; init; }
    public List<ValidationError>? Errors { get; init; }
}

public record ValidationError
{
    public string? Field { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }
    public object? AttemptedValue { get; init; }
    public string? Severity { get; init; }
}
