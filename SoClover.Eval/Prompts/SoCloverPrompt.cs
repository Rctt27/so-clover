namespace SoClover.Eval.Prompts;

/// <summary>
/// Les prompts que le harnais sait résoudre. <see cref="RepoPath"/> est relatif à la racine du
/// dépôt (source de <c>langfuse-sync</c> et cible de <c>langfuse-pull</c>) ;
/// <see cref="PackagedPath"/> est la copie embarquée dans l'output (source <c>file</c>).
/// Un <see cref="LangfuseName"/> nul désigne un prompt que Langfuse ne gère pas (spec §6.2).
/// </summary>
public sealed record SoCloverPrompt(
    string? LangfuseName,
    string Language,
    string FileName,
    string PackagedRelativeDirectory,
    string RepoPath)
{
    public string PackagedPath =>
        Path.Combine(AppContext.BaseDirectory, PackagedRelativeDirectory, Language, FileName);

    private static readonly string GeneratorDirectory = Path.Combine("Infrastructure", "AI", "Prompts");
    private static readonly string DecoderDirectory = Path.Combine("Decoder", "Prompts");

    public static readonly SoCloverPrompt GeneratorFrPerDirection = new(
        "generator-fr-per-direction", "fr", "board-clues-per-direction.md", GeneratorDirectory,
        "SoClover/Infrastructure/AI/Prompts/fr/board-clues-per-direction.md");

    public static readonly SoCloverPrompt GeneratorFrPerDirectionReasoning = new(
        null, "fr", "board-clues-per-direction.reasoning.md", GeneratorDirectory,
        "SoClover/Infrastructure/AI/Prompts/fr/board-clues-per-direction.reasoning.md");

    public static readonly SoCloverPrompt DecoderFrClue = new(
        "decoder-fr-clue", "fr", "decode-clue.md", DecoderDirectory,
        "SoClover.Eval/Decoder/Prompts/fr/decode-clue.md");

    public static readonly SoCloverPrompt DecoderFrBoard = new(
        "decoder-fr-board", "fr", "decode-board.md", DecoderDirectory,
        "SoClover.Eval/Decoder/Prompts/fr/decode-board.md");

    public static IReadOnlyList<SoCloverPrompt> Synchronized { get; } =
        [GeneratorFrPerDirection, DecoderFrClue, DecoderFrBoard];

    public static SoCloverPrompt ByLangfuseName(string name) =>
        Synchronized.SingleOrDefault(p => p.LangfuseName == name)
        ?? throw new ArgumentException(
            $"Prompt inconnu : {name}. Connus : {string.Join(", ", Synchronized.Select(p => p.LangfuseName))}.");
}
