using SoClover.Eval.Io;
using SoClover.Infrastructure.AI.Prompts;

namespace SoClover.Eval.Prompts;

/// <summary>
/// Toute comparaison de contenu de prompt passe par ici. Un fichier extrait sous Windows porte des
/// CRLF, Langfuse rend des LF : sans normalisation, un même prompt aurait deux sha et la garde 0
/// verrait des conflits qui n'existent pas.
/// </summary>
public static class PromptContent
{
    public static string Normalize(string content) => content.Replace("\r\n", "\n");

    public static string Sha256(string content) => EvalJson.Sha256Hex(Normalize(content));

    public static int? DeclaredVersion(string content) => FilePromptLoader.Parse(Normalize(content)).Version;
}
