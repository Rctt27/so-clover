using SoClover.Eval.Prompts;

namespace SoClover.Eval.Langfuse;

/// <summary>Ligne de <c>doctor</c> : le label production de Langfuse sert-il le prompt embarqué dans le build ?</summary>
public static class PromptDrift
{
    public static string Describe(SoCloverPrompt prompt, string packagedContent, LangfusePrompt? production)
    {
        var local = PromptContent.DeclaredVersion(packagedContent);
        if (production is null)
            return $"  {prompt.LangfuseName} : absent de Langfuse — lancer `langfuse-sync --prompts`";

        return PromptContent.Sha256(packagedContent) == PromptContent.Sha256(production.Content)
            ? $"  {prompt.LangfuseName} : production #{production.Version} = fichier embarqué (v{local})"
            : $"  {prompt.LangfuseName} : ÉCART — production #{production.Version} " +
              $"(v{PromptContent.DeclaredVersion(production.Content)}) ≠ fichier embarqué (v{local})";
    }

    /// <summary>
    /// Ligne de <c>doctor</c> sans clé Langfuse. Avec la source <c>langfuse</c> configurée, l'absence
    /// de clé n'est pas un détail : pas de repli silencieux (spec §6.5), donc les verbes qui
    /// résolvent un prompt échoueront — autant le dire ici plutôt qu'au premier run.
    /// </summary>
    public static string DescribeMissingCredentials(PromptSource source) => source == PromptSource.Langfuse
        ? "Langfuse : aucune clé configurée alors que la source des prompts est langfuse — contrôle de " +
          "dérive sauté, et `generate`, `decode` et `calibrate` échoueront tant que les clés manquent " +
          "(LANGFUSE__PUBLICKEY / LANGFUSE__SECRETKEY), ou passer `--prompt-source file`."
        : "Langfuse : aucune clé configurée — contrôle de dérive sauté.";
}
