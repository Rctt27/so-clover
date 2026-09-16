using SoClover.Eval.Cli;
using SoClover.Eval.Config;
using SoClover.Eval.Prompts;

namespace SoClover.Eval.Langfuse;

/// <summary>
/// Verbe <c>langfuse-sync</c> : pousse l'état du dépôt vers Langfuse. Idempotent — relancer ne
/// crée rien de nouveau. Se lance depuis la racine du dépôt (chemins <see cref="SoCloverPrompt.RepoPath"/>).
/// </summary>
public static class LangfuseSyncCommand
{
    public static async Task<int> ExecuteAsync(Args args, CancellationToken ct)
    {
        var syncPrompts = args.Has("prompts");
        if (!syncPrompts)
            throw new ArgumentException("langfuse-sync attend --prompts.");

        var client = LangfuseClientFactory.CreateRequired(
            EvalLlmConfig.BindLangfuse(EvalLlmConfig.BuildConfiguration()));

        return await SyncPromptsAsync(client, ct).ConfigureAwait(false);
    }

    private static async Task<int> SyncPromptsAsync(LangfuseClient client, CancellationToken ct)
    {
        var conflicts = 0;
        foreach (var prompt in SoCloverPrompt.Synchronized)
        {
            if (!File.Exists(prompt.RepoPath))
                throw new FileNotFoundException(
                    $"{prompt.RepoPath} introuvable : lancer langfuse-sync depuis la racine du dépôt.", prompt.RepoPath);

            var local = File.ReadAllText(prompt.RepoPath);
            var name = prompt.LangfuseName!;
            var history = await client.GetPromptHistoryAsync(name, ct).ConfigureAwait(false);
            var decision = PromptSyncPlanner.Decide(local, history);

            switch (decision.Outcome)
            {
                case PromptSyncOutcome.AlreadyPresent:
                    Console.WriteLine($"  {name} : v{decision.DeclaredVersion} déjà publié (#{decision.LangfuseVersion})");
                    break;

                case PromptSyncOutcome.Create:
                    var created = await client.CreatePromptAsync(
                        name,
                        PromptContent.Normalize(local),
                        [$"v{decision.DeclaredVersion}", "production"],
                        decision.DeclaredVersion,
                        $"sync git {prompt.RepoPath}",
                        ct).ConfigureAwait(false);
                    Console.WriteLine($"  {name} : v{decision.DeclaredVersion} publié (#{created.Version}, label production)");
                    break;

                case PromptSyncOutcome.Conflict:
                    conflicts++;
                    Console.Error.WriteLine(
                        $"  {name} : CONFLIT — le dépôt déclare version: {decision.DeclaredVersion} mais " +
                        $"Langfuse #{decision.LangfuseVersion} porte un autre contenu sous ce numéro. " +
                        "Bumper le frontmatter du fichier (garde 0), ne rien écraser.");
                    break;
            }
        }

        return conflicts == 0 ? 0 : 1;
    }
}
