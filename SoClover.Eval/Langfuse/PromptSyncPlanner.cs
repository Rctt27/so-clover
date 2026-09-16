using SoClover.Eval.Prompts;

namespace SoClover.Eval.Langfuse;

public enum PromptSyncOutcome
{
    AlreadyPresent,
    Create,
    Conflict,
}

public sealed record PromptSyncDecision(PromptSyncOutcome Outcome, int DeclaredVersion, int? LangfuseVersion);

/// <summary>
/// Git fait foi pour l'initialisation (spec §6.6) : on publie le contenu courant du dépôt, jamais
/// deux fois, et jamais sous un <c>version:</c> déjà pris par un autre contenu.
/// </summary>
public static class PromptSyncPlanner
{
    public static PromptSyncDecision Decide(string localContent, IReadOnlyList<LangfusePrompt> history)
    {
        var declared = PromptContent.DeclaredVersion(localContent)
            ?? throw new InvalidOperationException("Le fichier local ne déclare aucun `version:` lisible.");
        var sha = PromptContent.Sha256(localContent);

        if (history.FirstOrDefault(p => PromptContent.Sha256(p.Content) == sha) is { } same)
            return new PromptSyncDecision(PromptSyncOutcome.AlreadyPresent, declared, same.Version);

        if (history.FirstOrDefault(p => PromptContent.DeclaredVersion(p.Content) == declared) is { } clash)
            return new PromptSyncDecision(PromptSyncOutcome.Conflict, declared, clash.Version);

        return new PromptSyncDecision(PromptSyncOutcome.Create, declared, null);
    }
}
