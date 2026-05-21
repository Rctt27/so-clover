using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class LlmConfigurationTests
{
    private static IServiceProvider BuildProvider(IDictionary<string, string?> envVars)
    {
        // Build a configuration mimicking what the host does at startup:
        //   1. appsettings.json (base) — loaded from disk for fidelity.
        //   2. Environment variables (LLM__*) override.
        var appSettingsPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "appsettings.json"));

        var config = new ConfigurationBuilder()
            .AddJsonFile(appSettingsPath, optional: false)
            .AddInMemoryCollection(envVars) // simulates env vars (binding key uses ":" or "__"; we use ":")
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<LlmOptions>()
            .Bind(config.GetSection(LlmOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<LlmOptions>, LlmOptionsValidator>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Defaults_from_appsettings_are_loaded()
    {
        var sp = BuildProvider(new Dictionary<string, string?>());

        var opts = sp.GetRequiredService<IOptions<LlmOptions>>().Value;

        Assert.Equal(LlmProvider.OpenAI, opts.Provider);
        Assert.Equal("http://localhost:1234/v1", opts.BaseUrl);
        Assert.Equal(4, opts.MaxConcurrency);
        Assert.Equal(200, opts.MaxCallsPerGame);
    }

    [Fact]
    public void Env_var_LLM_MAXCONCURRENCY_overrides_appsettings()
    {
        var sp = BuildProvider(new Dictionary<string, string?>
        {
            // ":" is what AddInMemoryCollection uses; the host's EnvVar source maps "LLM__MAXCONCURRENCY" → "Llm:MaxConcurrency".
            ["Llm:MaxConcurrency"] = "8"
        });

        var opts = sp.GetRequiredService<IOptions<LlmOptions>>().Value;

        Assert.Equal(8, opts.MaxConcurrency);
    }

    [Fact]
    public void Env_var_LLM_PROVIDER_Anthropic_overrides_default()
    {
        var sp = BuildProvider(new Dictionary<string, string?>
        {
            ["Llm:Provider"] = "Anthropic",
            ["Llm:ApiKey"] = "sk-ant-test",
            ["Llm:DefaultModel"] = "claude-haiku-4-6"
        });

        var opts = sp.GetRequiredService<IOptions<LlmOptions>>().Value;

        Assert.Equal(LlmProvider.Anthropic, opts.Provider);
        Assert.Equal("sk-ant-test", opts.ApiKey);
        Assert.Equal("claude-haiku-4-6", opts.DefaultModel);
    }

    [Fact]
    public void MaxConcurrency_above_16_fails_validation_at_resolve_time()
    {
        var sp = BuildProvider(new Dictionary<string, string?>
        {
            ["Llm:MaxConcurrency"] = "20"
        });

        // ValidateOnStart() runs at host startup; with IOptions, validation runs lazily on first access.
        // Use IOptionsMonitor / direct validator for a synchronous check, or trigger validation by accessing .Value.
        var ex = Assert.Throws<OptionsValidationException>(() =>
            _ = sp.GetRequiredService<IOptions<LlmOptions>>().Value);
        Assert.Contains("MaxConcurrency", ex.Message);
    }
}