using System.Text;
using SoClover.Eval.Langfuse;

namespace SoClover.Eval.Prompts;

/// <summary>
/// Résoudre → matérialiser → charger (spec §6.1). Le prompt Langfuse est écrit sous
/// <c>&lt;root&gt;/&lt;sha12&gt;/&lt;langue&gt;/&lt;fichier d'origine&gt;</c> : les deux derniers
/// segments reproduisent le chemin d'origine, ce que lit <c>DecoderFingerprint.CanonicalPromptPath</c>.
/// Un contenu identique au fichier embarqué donne donc la même empreinte.
/// </summary>
public sealed class PromptResolver
{
    public static readonly string DefaultRoot = Path.Combine("eval", "prompts", "resolved");

    private readonly LangfuseClient? _client;
    private readonly string _root;

    public PromptResolver(LangfuseClient? client, string root)
    {
        _client = client;
        _root = root;
    }

    public async Task<ResolvedPrompt> ResolveAsync(SoCloverPrompt prompt, PromptSelection selection, CancellationToken ct)
    {
        if (selection.Source == PromptSource.File)
        {
            var local = File.ReadAllText(prompt.PackagedPath);
            return new ResolvedPrompt(
                prompt.PackagedPath,
                PromptContent.DeclaredVersion(local),
                new PromptProvenance("file", null, null, null, PromptContent.Sha256(local)));
        }

        if (prompt.LangfuseName is null)
            throw new InvalidOperationException(
                $"{prompt.FileName} n'est pas géré dans Langfuse (spec §6.2) : passer --prompt-source file.");

        if (_client is null)
            throw new InvalidOperationException(
                "Source de prompts langfuse sans clés : renseigner LANGFUSE__PUBLICKEY et LANGFUSE__SECRETKEY, " +
                "ou passer --prompt-source file.");

        var what = selection.Version is { } v ? $"version #{v}" : $"label {selection.Label ?? "production"}";
        var resolved = await _client.GetPromptAsync(prompt.LangfuseName, selection.Label, selection.Version, ct)
                           .ConfigureAwait(false)
                       ?? throw new InvalidOperationException(
                           $"{prompt.LangfuseName} ({what}) introuvable dans Langfuse. " +
                           "Première utilisation : lancer `langfuse-sync --prompts`.");

        var history = await _client.GetPromptHistoryAsync(prompt.LangfuseName, ct).ConfigureAwait(false);
        var declared = PromptVersionGuard.RequireConsistent(resolved, history);

        var content = PromptContent.Normalize(resolved.Content);
        var sha = PromptContent.Sha256(content);
        var path = Materialize(prompt, content, sha);

        return new ResolvedPrompt(
            path,
            declared,
            new PromptProvenance("langfuse", resolved.Name, resolved.Version, selection.Version is null ? selection.Label : null, sha));
    }

    private string Materialize(SoCloverPrompt prompt, string content, string sha)
    {
        var directory = Path.Combine(_root, sha[..12], prompt.Language);
        var path = Path.Combine(directory, prompt.FileName);

        if (File.Exists(path))
        {
            if (PromptContent.Normalize(File.ReadAllText(path)) != content)
                throw new InvalidOperationException(
                    $"{path} existe mais ne correspond plus au sha {sha[..12]} : fichier matérialisé altéré. " +
                    "Le supprimer, il sera réécrit depuis Langfuse.");
            return path;
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }
}
