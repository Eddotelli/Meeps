namespace API.Infrastructure.Configuration;

public class GoogleAISettings
{
    public string Model { get; set; } = "gemini-2.5-flash-image";
    public string ApiKey { get; set; } = string.Empty;
}
