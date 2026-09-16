using SoClover.Eval.Cli;
using SoClover.Eval.Langfuse;

namespace SoClover.Eval.Prompts;

/// <summary>
/// Drapeaux de sélection du prompt, communs à <c>generate</c>, <c>decode</c> et <c>calibrate</c>.
/// Comme <see cref="Args.GetInt"/> : une valeur présente mais mal formée échoue fermé.
/// </summary>
public static class PromptSelectionArgs
{
    public static PromptSelection From(Args args, LangfuseOptions options)
    {
        var source = args.Get("prompt-source") switch
        {
            null => options.PromptSource,
            "file" => PromptSource.File,
            "langfuse" => PromptSource.Langfuse,
            var other => throw new ArgumentException(
                $"--prompt-source attend file ou langfuse, valeur reçue : \"{other}\""),
        };

        var label = args.Get("prompt-label");
        int? version = args.Has("prompt-version") ? args.GetInt("prompt-version", 0) : null;

        if (label is not null && version is not null)
            throw new ArgumentException("--prompt-label et --prompt-version s'excluent.");

        if (source == PromptSource.File)
        {
            if (label is not null || version is not null)
                throw new ArgumentException(
                    "--prompt-label et --prompt-version n'ont de sens qu'avec la source langfuse.");
            return new PromptSelection(PromptSource.File, null, null);
        }

        return version is not null
            ? new PromptSelection(PromptSource.Langfuse, null, version)
            : new PromptSelection(PromptSource.Langfuse, label ?? options.PromptLabel, null);
    }

    public static PromptSelection ForBoardPrompt(PromptSelection clueSelection, LangfuseOptions options) =>
        clueSelection.Source == PromptSource.File
            ? clueSelection
            : new PromptSelection(PromptSource.Langfuse, clueSelection.Label ?? options.PromptLabel, null);
}
