using System.Text;
using SoClover.Eval.Cli;
using SoClover.Eval.Config;
using SoClover.Eval.Prompts;

namespace SoClover.Eval.Langfuse;

/// <summary>
/// Verbe <c>langfuse-pull</c> : écrit une version Langfuse dans le fichier du dépôt. Le commit reste
/// manuel, et doit embarquer l'alignement de <c>PackagedPromptGenerationTests</c> pour le générateur.
/// </summary>
public static class LangfusePullCommand
{
    public static async Task<int> ExecuteAsync(Args args, CancellationToken ct)
    {
        var prompt = SoCloverPrompt.ByLangfuseName(args.Require("prompt"));
        var label = args.Get("label");
        var hasVersion = args.Has("version");
        if ((label is null) == !hasVersion)
            throw new ArgumentException("langfuse-pull attend exactement un de --label ou --version.");
        int? version = hasVersion ? args.GetInt("version", 0) : null;

        var client = LangfuseClientFactory.CreateRequired(
            EvalLlmConfig.BindLangfuse(EvalLlmConfig.BuildConfiguration()));
        var name = prompt.LangfuseName!;

        var incoming = await client.GetPromptAsync(name, label, version, ct).ConfigureAwait(false)
                       ?? throw new InvalidOperationException($"{name} ({label ?? $"#{version}"}) introuvable dans Langfuse.");
        PromptVersionGuard.RequireConsistent(incoming, await client.GetPromptHistoryAsync(name, ct).ConfigureAwait(false));

        if (!File.Exists(prompt.RepoPath))
            throw new FileNotFoundException(
                $"{prompt.RepoPath} introuvable : lancer langfuse-pull depuis la racine du dépôt.", prompt.RepoPath);

        var next = PromptPullPolicy.Check(File.ReadAllText(prompt.RepoPath), incoming);
        File.WriteAllText(prompt.RepoPath, PromptContent.Normalize(incoming.Content), new UTF8Encoding(false));

        Console.WriteLine($"{prompt.RepoPath} ← {name} #{incoming.Version} (version: {next})");
        Console.WriteLine("À faire avant commit : `dotnet test`, et relire le diff.");
        if (prompt == SoCloverPrompt.GeneratorFrPerDirection)
            Console.WriteLine(
                "Prompt servi en PRODUCTION : PackagedPromptGenerationTests passe au rouge par construction — " +
                "l'aligner dans le même commit ; EN et PT gardent leur génération tant qu'ils ne rattrapent pas le FR.");
        return 0;
    }
}
