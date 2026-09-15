namespace SoClover.Eval.Langfuse;

public static class LangfuseClientFactory
{
    /// <summary>Client, ou <c>null</c> sans clés : c'est au consommateur de décider si l'absence est une erreur.</summary>
    public static LangfuseClient? CreateOrNull(LangfuseOptions options) =>
        options.HasCredentials
            ? new LangfuseClient(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, options)
            : null;

    public static LangfuseClient CreateRequired(LangfuseOptions options) =>
        CreateOrNull(options) ?? throw new InvalidOperationException(
            "Clés Langfuse absentes : renseigner LANGFUSE__PUBLICKEY et LANGFUSE__SECRETKEY " +
            "(variables d'environnement ou SoClover.Eval/evalsettings.local.json).");
}
