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
}
