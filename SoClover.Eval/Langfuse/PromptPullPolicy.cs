using SoClover.Eval.Prompts;

namespace SoClover.Eval.Langfuse;

/// <summary>
/// Retour vers le dépôt (spec §6.6). Un contenu différent n'entre dans git que sous un numéro
/// jamais utilisé : c'est la règle du bump de génération de CLAUDE.md, appliquée au geste de pull.
/// </summary>
public static class PromptPullPolicy
{
    public static int Check(string repoContent, LangfusePrompt incoming)
    {
        if (PromptContent.Sha256(repoContent) == PromptContent.Sha256(incoming.Content))
            throw new InvalidOperationException(
                $"{incoming.Name} #{incoming.Version} est identique au fichier du dépôt : rien à tirer.");

        var current = PromptContent.DeclaredVersion(repoContent)
            ?? throw new InvalidOperationException("Le fichier du dépôt ne déclare aucun `version:` lisible.");
        var next = PromptContent.DeclaredVersion(incoming.Content)
            ?? throw new InvalidOperationException(
                $"{incoming.Name} #{incoming.Version} ne déclare aucun `version:` lisible.");

        if (next <= current)
            throw new InvalidOperationException(
                $"{incoming.Name} #{incoming.Version} déclare version: {next}, le dépôt porte version: {current}. " +
                "Un contenu différent exige un numéro supérieur, jamais utilisé (garde 0).");

        return next;
    }
}
