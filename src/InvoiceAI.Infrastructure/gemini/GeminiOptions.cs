namespace InvoiceAI.Infrastructure.Gemini;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.8-flash";
    public string Endpoint { get; set; } = "https://generativelanguage.googleapis.com/v1beta/interactions";
    public string ApiRevision { get; set; } = "2026-05-20";
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxRetries { get; set; } = 3;
    public int RetryBaseSeconds { get; set; } = 15;
    public int DelayBetweenJobsSeconds { get; set; } = 5;
}