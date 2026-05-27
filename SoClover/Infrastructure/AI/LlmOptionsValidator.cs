using Microsoft.Extensions.Options;

namespace SoClover.Infrastructure.AI;

public sealed class LlmOptionsValidator : IValidateOptions<LlmOptions>
{
    public const int MaxAllowedConcurrency = 16;

    public ValidateOptionsResult Validate(string? name, LlmOptions options)
    {
        var failures = new List<string>();

        if (options.MaxConcurrency < 1 || options.MaxConcurrency > MaxAllowedConcurrency)
        {
            failures.Add(
                $"LlmOptions.MaxConcurrency must be in [1, {MaxAllowedConcurrency}] (got {options.MaxConcurrency}).");
        }

        if (options.MaxCallsPerGame < 1)
        {
            failures.Add(
                $"LlmOptions.MaxCallsPerGame must be >= 1 (got {options.MaxCallsPerGame}).");
        }

        if (options.TimeoutSeconds < 1)
        {
            failures.Add(
                $"LlmOptions.TimeoutSeconds must be >= 1 (got {options.TimeoutSeconds}).");
        }

        if (options.MaxRetries < 0)
        {
            failures.Add(
                $"LlmOptions.MaxRetries must be >= 0 (got {options.MaxRetries}).");
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            failures.Add("LlmOptions.BaseUrl must be a non-empty URL.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultModel))
        {
            failures.Add("LlmOptions.DefaultModel must be a non-empty model identifier.");
        }

        if (options.DefaultTemperature < 0.0 || options.DefaultTemperature > 2.0)
        {
            failures.Add(
                $"LlmOptions.DefaultTemperature must be in [0.0, 2.0] (got {options.DefaultTemperature}).");
        }

        if (options.TopP is { } topP && (topP <= 0.0 || topP > 1.0))
        {
            failures.Add(
                $"LlmOptions.TopP must be in (0.0, 1.0] when set (got {topP}).");
        }

        if (options.MaxOutputTokens is { } maxTokens && maxTokens < 1)
        {
            failures.Add(
                $"LlmOptions.MaxOutputTokens must be >= 1 when set (got {maxTokens}).");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}