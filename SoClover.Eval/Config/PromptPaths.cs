namespace SoClover.Eval.Config;

/// <summary>
/// Résolution du chemin de prompt réellement chargé, pour le manifeste de run.
/// <para>
/// Duplique volontairement la logique de <c>FrenchAiCluePromptProvider</c> : le provider ne
/// l'expose pas, et le manifeste doit consigner un chemin <b>observé</b>. Si la convention de
/// nommage change côté <c>SoClover</c>, ce fichier doit suivre — le test
/// <c>EvalLlmConfigTests.FrPerDirection_prompt_path_depends_on_the_reasoning_flag</c> vérifie
/// l'existence sur disque et échouera sinon.
/// </para>
/// </summary>
public static class PromptPaths
{
    public static string FrPerDirection(bool reasoningEnabled)
    {
        var fileName = reasoningEnabled
            ? "board-clues-per-direction.reasoning.md"
            : "board-clues-per-direction.md";
        return Path.Combine(
            AppContext.BaseDirectory, "Infrastructure", "AI", "Prompts", "fr", fileName);
    }
}
