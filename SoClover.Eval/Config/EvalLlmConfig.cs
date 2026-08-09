using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SoClover.Eval.Io;
using SoClover.Infrastructure.AI;

namespace SoClover.Eval.Config;

/// <summary>
/// Configuration du harnais : deux sections <c>Generator</c> et <c>Decoder</c>, chacune de la
/// forme <see cref="LlmOptions"/> — donc surchargeables par <c>GENERATOR__*</c> / <c>DECODER__*</c>
/// et validables par le <see cref="LlmOptionsValidator"/> de production. Aucun second modèle de
/// configuration à inventer ; les secrets restent hors du repo.
/// </summary>
public static class EvalLlmConfig
{
    /// <summary>
    /// Le fichier s'appelle <c>evalsettings.json</c> et non <c>appsettings.json</c> : voir le
    /// commentaire de <c>SoClover.Eval.csproj</c> — sous ce dernier nom, il écrasait celui de
    /// <c>SoClover</c> dans l'output de <c>SoClover.Tests</c> par propagation transitive.
    /// </summary>
    public static IConfigurationRoot BuildConfiguration() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("evalsettings.json", optional: false)
            .AddJsonFile("evalsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

    public static IOptions<LlmOptions> Bind(IConfiguration config, string sectionName)
    {
        var section = config.GetSection(sectionName);
        if (!section.Exists())
            throw new InvalidOperationException(
                $"Section de configuration « {sectionName} » absente de evalsettings.json.");

        var options = new LlmOptions();
        section.Bind(options);

        var validation = new LlmOptionsValidator().Validate(sectionName, options);
        if (validation.Failed)
            throw new InvalidOperationException(
                $"Section « {sectionName} » invalide :{Environment.NewLine}" +
                string.Join(Environment.NewLine, validation.Failures ?? []));

        return Options.Create(options);
    }

    /// <summary>
    /// Compose le même pipeline que la production : <c>Timeout(Throttle(Provider))</c>.
    /// </summary>
    public static IChatClient CreateChatClient(IOptions<LlmOptions> options) =>
        new ChatClientFactory(options).Create();

    /// <summary>
    /// SHA-256 du payload de <c>GET {baseUrl}/models</c>, tronqué à 12 caractères.
    /// <para>
    /// Si l'endpoint est absent ou injoignable (provider Anthropic, LM Studio arrêté entre-temps),
    /// rend <c>null</c> avec un avertissement — jamais une erreur fatale. Utile, mais
    /// <b>il ne capture pas</b> le toggle « enable thinking » de LM Studio, appliqué au chargement
    /// du modèle — précisément le réglage qui a déjà fait dériver des runs. D'où <c>operatorNotes</c>.
    /// </para>
    /// </summary>
    public static async Task<string?> FetchProviderModelListHashAsync(
        LlmOptions options, CancellationToken ct)
    {
        if (options.Provider != LlmProvider.OpenAI)
            return null;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url = options.BaseUrl.TrimEnd('/') + "/models";
            var payload = await http.GetStringAsync(url, ct).ConfigureAwait(false);
            return EvalJson.Sha256Hex(payload)[..12];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            Console.Error.WriteLine(
                $"AVERTISSEMENT : impossible de lire {options.BaseUrl}/models ({ex.Message}). " +
                "providerModelListHash sera null.");
            return null;
        }
    }
}
