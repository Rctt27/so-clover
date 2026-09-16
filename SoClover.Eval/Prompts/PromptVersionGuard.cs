using SoClover.Eval.Langfuse;

namespace SoClover.Eval.Prompts;

/// <summary>
/// Garde 0 (skill soclover-eval) appliquée par une machine. <c>DecoderFingerprint</c> hache la
/// version <b>déclarée</b>, jamais le contenu : un prompt édité dans l'UI de Langfuse sans bump de
/// son frontmatter garderait son empreinte, et deux décodeurs différents écriraient sous la même
/// identité. On inspecte donc tout l'historique avant d'exécuter quoi que ce soit.
/// </summary>
public static class PromptVersionGuard
{
    public static int RequireConsistent(LangfusePrompt resolved, IReadOnlyList<LangfusePrompt> history)
    {
        var declared = PromptContent.DeclaredVersion(resolved.Content)
            ?? throw new InvalidOperationException(
                $"{resolved.Name} #{resolved.Version} (Langfuse) ne déclare aucun `version:` lisible " +
                "dans son frontmatter : impossible de l'identifier dans le registre.");

        var sha = PromptContent.Sha256(resolved.Content);
        var conflicts = history
            .Where(p => p.Version != resolved.Version
                        && PromptContent.DeclaredVersion(p.Content) == declared
                        && PromptContent.Sha256(p.Content) != sha)
            .Select(p => $"#{p.Version}")
            .ToList();

        if (conflicts.Count > 0)
            throw new InvalidOperationException(
                $"{resolved.Name} : les versions Langfuse #{resolved.Version} et {string.Join(", ", conflicts)} " +
                $"déclarent toutes `version: {declared}` avec des contenus différents. Un contenu modifié " +
                "exige un numéro jamais utilisé (garde 0) : publier une nouvelle version au frontmatter bumpé.");

        return declared;
    }
}
