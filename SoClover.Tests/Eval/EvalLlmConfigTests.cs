using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SoClover.Eval.Config;
using SoClover.Infrastructure.AI;
using Xunit;

namespace SoClover.Tests.Eval;

public class EvalLlmConfigTests
{
    private static IConfiguration InMemory(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Binds_a_section_into_LlmOptions()
    {
        var config = InMemory(new()
        {
            ["Generator:provider"] = "OpenAI",
            ["Generator:baseUrl"] = "http://localhost:1234/v1",
            ["Generator:apiKey"] = "lm-studio",
            ["Generator:defaultModel"] = "gemma-4-12b-qat",
            ["Generator:defaultTemperature"] = "1.0",
            ["Generator:topP"] = "0.95",
            ["Generator:maxOutputTokens"] = "4096",
            ["Generator:maxRetries"] = "0",
            ["Generator:generationMode"] = "PerDirection",
        });

        var opts = EvalLlmConfig.Bind(config, "Generator").Value;

        Assert.Equal(LlmProvider.OpenAI, opts.Provider);
        Assert.Equal("gemma-4-12b-qat", opts.DefaultModel);
        Assert.Equal(1.0, opts.DefaultTemperature);
        Assert.Equal(0.95, opts.TopP);
        Assert.Equal(4096, opts.MaxOutputTokens);
        Assert.Equal(0, opts.MaxRetries);
        Assert.Equal(AiClueGenerationMode.PerDirection, opts.GenerationMode);
    }

    [Fact]
    public void Generator_and_Decoder_are_independent_sections()
    {
        var config = InMemory(new()
        {
            ["Generator:defaultModel"] = "modele-generateur",
            ["Decoder:defaultModel"] = "modele-decodeur",
        });

        Assert.Equal("modele-generateur", EvalLlmConfig.Bind(config, "Generator").Value.DefaultModel);
        Assert.Equal("modele-decodeur", EvalLlmConfig.Bind(config, "Decoder").Value.DefaultModel);
    }

    // Réutilise le validateur de production : aucun second modèle de configuration à inventer.
    [Fact]
    public void Rejects_a_section_that_fails_the_production_validator()
    {
        var config = InMemory(new()
        {
            ["Generator:defaultModel"] = "m",
            ["Generator:maxConcurrency"] = "0",
        });

        var ex = Assert.Throws<InvalidOperationException>(() => EvalLlmConfig.Bind(config, "Generator"));
        Assert.Contains("MaxConcurrency", ex.Message);
    }

    [Fact]
    public void Rejects_a_missing_section()
    {
        var config = InMemory(new() { ["Generator:defaultModel"] = "m" });

        var ex = Assert.Throws<InvalidOperationException>(() => EvalLlmConfig.Bind(config, "Absente"));
        Assert.Contains("Absente", ex.Message);
    }

    [Fact]
    public void Environment_variables_override_the_json_section()
    {
        Environment.SetEnvironmentVariable("GENERATOR__DEFAULTMODEL", "modele-par-env");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Generator:defaultModel"] = "modele-json",
                })
                .AddEnvironmentVariables()
                .Build();

            Assert.Equal("modele-par-env", EvalLlmConfig.Bind(config, "Generator").Value.DefaultModel);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GENERATOR__DEFAULTMODEL", null);
        }
    }

    // Le fichier de config du harnais s'appelle evalsettings.json et NON appsettings.json.
    // Sous ce dernier nom, il gagnait la course de copie contre celui de SoClover dans l'output
    // partagé de SoClover.Tests (propagation transitive des Content) et faisait disparaître
    // GameDefaults / Llm / AIPlayers des tests d'intégration HTTP, silencieusement. Ce test
    // verrouille les deux moitiés de l'invariant : l'éval lit bien ses sections, et le fichier
    // de production reste intact à côté.
    [Fact]
    public void BuildConfiguration_reads_evalsettings_without_shadowing_the_production_appsettings()
    {
        var config = EvalLlmConfig.BuildConfiguration();

        Assert.True(config.GetSection("Generator").Exists());
        Assert.True(config.GetSection("Decoder").Exists());

        using var production = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        Assert.True(production.RootElement.TryGetProperty("AIPlayers", out _));
    }

    [Fact]
    public void FrPerDirection_prompt_path_depends_on_the_reasoning_flag()
    {
        var standard = PromptPaths.FrPerDirection(reasoningEnabled: false);
        var reasoning = PromptPaths.FrPerDirection(reasoningEnabled: true);

        Assert.EndsWith(Path.Combine("fr", "board-clues-per-direction.md"), standard);
        Assert.EndsWith(Path.Combine("fr", "board-clues-per-direction.reasoning.md"), reasoning);
        Assert.True(File.Exists(standard), $"prompt introuvable : {standard}");
        Assert.True(File.Exists(reasoning), $"prompt reasoning introuvable : {reasoning}");
    }
}
