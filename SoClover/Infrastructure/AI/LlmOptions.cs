namespace SoClover.Infrastructure.AI;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public LlmProvider Provider { get; set; } = LlmProvider.OpenAI;
    public string BaseUrl { get; set; } = "http://localhost:1234/v1";
    public string ApiKey { get; set; } = "lm-studio";
    public string DefaultModel { get; set; } = "lmstudio-community/Meta-Llama-3.1-8B-Instruct-GGUF";
    public double DefaultTemperature { get; set; } = 0.7;
    public int MaxRetries { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxConcurrency { get; set; } = 4;
    public int MaxCallsPerGame { get; set; } = 200;
}