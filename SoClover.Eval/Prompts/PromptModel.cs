using SoClover.Eval.Langfuse;

namespace SoClover.Eval.Prompts;

/// <summary><see cref="Version"/> et <see cref="Label"/> s'excluent ; ils n'ont de sens que pour la source Langfuse.</summary>
public sealed record PromptSelection(PromptSource Source, string? Label, int? Version);

/// <summary>
/// D'où vient le prompt d'un run. Part aux manifestes <b>hors empreinte</b> et hors hash8 : elle dit
/// ce qui a été servi, l'identité du prompt reste son chemin canonique et sa version déclarée.
/// </summary>
public sealed record PromptProvenance(
    string Source,
    string? LangfuseName,
    int? LangfuseVersion,
    string? LangfuseLabel,
    string ContentSha256);

public sealed record ResolvedPrompt(string Path, int? DeclaredVersion, PromptProvenance Provenance)
{
    public string Describe()
    {
        var sha = Provenance.ContentSha256[..8];
        if (Provenance.Source != "langfuse")
            return $"v{DeclaredVersion} (fichier, sha {sha})";

        var label = Provenance.LangfuseLabel is { } l ? $", label {l}" : string.Empty;
        return $"v{DeclaredVersion} (langfuse {Provenance.LangfuseName} #{Provenance.LangfuseVersion}{label}, sha {sha})";
    }
}
