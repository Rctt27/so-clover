using System.Diagnostics;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Cli;
using SoClover.Eval.Config;
using SoClover.Eval.Io;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Prompts;
using SoClover.Infrastructure.AI;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Infrastructure.Validation;

namespace SoClover.Eval.Runner;

/// <summary>
/// Verbe <c>generate</c> : banc → indices. Exécution <b>séquentielle</b> (LM Studio,
/// <c>maxConcurrency=1</c>), écriture au fil de l'eau, reprise sur relance.
/// </summary>
public static class GenerateCommand
{
    /// <summary>
    /// Directions restant à traiter, dans l'ordre board par board puis direction canonique.
    /// Une direction est terminée si elle a une tentative valide, ou <paramref name="maxAttempts"/>
    /// tentatives consignées.
    /// </summary>
    public static IReadOnlyList<(BenchBoard Board, Direction Direction)> PendingWork(
        BenchContents bench, RunContents? existing, int maxAttempts)
    {
        var completed = existing is null
            ? new HashSet<(string, string)>()
            : RunFile.CompletedDirections(existing, maxAttempts).ToHashSet();

        var pending = new List<(BenchBoard, Direction)>();
        foreach (var board in bench.Boards)
        {
            foreach (var direction in BoardGeometry.AllDirections)
            {
                if (!completed.Contains((board.BoardId, direction.ToString())))
                    pending.Add((board, direction));
            }
        }
        return pending.AsReadOnly();
    }

    public static async Task<int> ExecuteAsync(Args args, CancellationToken ct)
    {
        var benchPath = args.Require("bench");
        var notes = args.Get("notes");
        var force = args.Has("force");
        var outDirectory = args.Get("out-dir") ?? Path.Combine("eval", "runs");
        var modelSnapshotDate = args.Get("model-snapshot") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");

        var bench = BenchFile.Read(benchPath);
        Console.WriteLine($"banc : {benchPath} ({bench.Manifest.BoardCount} boards, hash {bench.Manifest.BenchHash})");

        // Avant toute résolution de configuration LLM : le plancher n'appelle aucun modèle et doit
        // donc rester produisible sans provider joignable.
        if (args.Has("random-baseline"))
            return await ExecuteRandomBaselineAsync(args, bench, ct).ConfigureAwait(false);

        var config = EvalLlmConfig.BuildConfiguration();
        var llmOptions = EvalLlmConfig.Bind(config, "Generator");
        var opts = llmOptions.Value;

        if (opts.GenerationMode != AiClueGenerationMode.PerDirection)
            throw new InvalidOperationException(
                $"Seul PerDirection est évalué dans ce cycle (Generator:generationMode={opts.GenerationMode}).");

        // Résolution AVANT tout appel au LLM : une source langfuse indisponible arrête la commande
        // sans rien dépenser, et sans repli silencieux sur le fichier (spec §6.5).
        var langfuseOptions = EvalLlmConfig.BindLangfuse(config);
        var selection = PromptSelectionArgs.From(args, langfuseOptions);
        var catalogPrompt = opts.ReasoningEnabled
            ? SoCloverPrompt.GeneratorFrPerDirectionReasoning
            : SoCloverPrompt.GeneratorFrPerDirection;
        var resolver = new PromptResolver(LangfuseClientFactory.CreateOrNull(langfuseOptions), PromptResolver.DefaultRoot);
        var generatorPrompt = await resolver.ResolveAsync(catalogPrompt, selection, ct).ConfigureAwait(false);

        using var chatClient = EvalLlmConfig.CreateChatClient(llmOptions);
        var promptProvider = new FrenchAiCluePromptProvider(
            new FilePromptLoader(),
            PromptPaths.FrBoardClues(),
            opts.ReasoningEnabled ? SoCloverPrompt.GeneratorFrPerDirection.PackagedPath : generatorPrompt.Path,
            opts.ReasoningEnabled ? generatorPrompt.Path : SoCloverPrompt.GeneratorFrPerDirectionReasoning.PackagedPath);
        var validator = new ClueValidatorFactory().GetFor(bench.Manifest.Language, semanticCheckEnabled: true);
        var caller = new AiClueLlmCaller(chatClient, llmOptions);
        var maxAttempts = opts.MaxRetries + 1;

        // Le promptVersion n'est connu qu'après construction d'un prompt : on en bâtit un
        // pour le premier board du banc, avant d'ouvrir le fichier de run.
        var probe = promptProvider.BuildSingleDirectionCluePrompt(new BoardCluesPromptContext(
            bench.Manifest.Language,
            BenchBoardMapper.ToSnapshots(bench.Boards[0]),
            [Direction.Top],
            new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>(),
            IncludeReasoning: opts.ReasoningEnabled));

        var providerModelListHash = await EvalLlmConfig
            .FetchProviderModelListHashAsync(opts, ct).ConfigureAwait(false);

        var createdAtUtc = DateTime.UtcNow;
        var draftManifest = new RunManifest(
            Kind: "manifest",
            RunId: string.Empty,
            Stage: "generate",
            CreatedAtUtc: createdAtUtc,
            BenchFile: benchPath.Replace('\\', '/'),
            BenchHash: bench.Manifest.BenchHash,
            PromptFile: generatorPrompt.Path.Replace('\\', '/'),
            PromptVersion: probe.PromptVersion,
            GenerationMode: opts.GenerationMode.ToString(),
            ReasoningEnabled: opts.ReasoningEnabled,
            Provider: opts.Provider.ToString(),
            BaseUrl: opts.BaseUrl,
            ModelId: opts.DefaultModel,
            ModelSnapshotDate: modelSnapshotDate,
            ProviderModelListHash: providerModelListHash,
            Temperature: opts.DefaultTemperature,
            TopP: opts.TopP,
            MaxOutputTokens: opts.MaxOutputTokens,
            MaxRetries: opts.MaxRetries,
            Language: bench.Manifest.Language,
            HarnessVersion: RunFile.HarnessVersion,
            OperatorNotes: notes,
            Prompt: generatorPrompt.Provenance);

        var hash8 = RunFile.ComputeHash8(draftManifest);
        var runId = args.Get("run-id")
            ?? RunFile.BuildRunId(createdAtUtc, probe.PromptVersion, opts.DefaultModel, hash8);
        var manifest = draftManifest with { RunId = runId };
        var runPath = Path.Combine(outDirectory, $"{runId}.jsonl");

        RunContents? existing = null;
        if (File.Exists(runPath) && !force)
        {
            existing = RunFile.Read(runPath);
            if (existing.Manifest.BenchHash != bench.Manifest.BenchHash)
                throw new InvalidOperationException(
                    $"{runPath} porte benchHash {existing.Manifest.BenchHash}, incompatible avec {bench.Manifest.BenchHash}.");
            Console.WriteLine($"reprise : {existing.Attempts.Count} tentative(s) déjà consignée(s)");
        }
        else
        {
            RunFile.WriteManifest(runPath, manifest);
        }

        var pending = PendingWork(bench, existing, maxAttempts);
        var expected = bench.Manifest.BoardCount * 4;
        Console.WriteLine($"run : {runId}");
        Console.WriteLine($"  fichier      : {runPath}");
        Console.WriteLine($"  modèle       : {opts.DefaultModel} (snapshot {modelSnapshotDate})");
        Console.WriteLine($"  prompt       : {generatorPrompt.Describe()}, reasoning={opts.ReasoningEnabled}");
        Console.WriteLine($"  à traiter    : {pending.Count} direction(s) sur {expected}");
        if (notes is not null) Console.WriteLine($"  notes        : {notes}");

        var runner = new ClueRunner(caller, promptProvider, validator, maxAttempts, bench.Manifest.Language);
        var stopwatch = Stopwatch.StartNew();
        var done = 0;

        foreach (var (board, direction) in pending)
        {
            ct.ThrowIfCancellationRequested();

            await foreach (var attempt in runner.RunDirectionAsync(board, direction, ct).ConfigureAwait(false))
                RunFile.AppendAttempt(runPath, attempt);

            done++;
            var elapsed = stopwatch.Elapsed;
            var eta = done > 0 ? TimeSpan.FromSeconds(elapsed.TotalSeconds / done * (pending.Count - done)) : TimeSpan.Zero;
            Console.WriteLine(
                $"  [{done}/{pending.Count}] {board.BoardId} {direction} — écoulé {elapsed:hh\\:mm\\:ss}, reste ~{eta:hh\\:mm\\:ss}");
        }

        Console.WriteLine($"terminé en {stopwatch.Elapsed:hh\\:mm\\:ss}. Run : {runPath}");
        return 0;
    }

    // Plancher aléatoire : aucun appel LLM, aucun code de scoring dédié. Le run produit
    // se décode et se score comme n'importe quel autre.
    private static async Task<int> ExecuteRandomBaselineAsync(
        Args args, BenchContents bench, CancellationToken ct)
    {
        var seed = args.GetLong("seed", 20260727000);
        var outDirectory = args.Get("out-dir") ?? Path.Combine("eval", "runs");
        var notes = args.Get("notes");

        var dictionaryDir = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Dictionaries");
        var words = await new SoClover.Infrastructure.FileWordDictionary(dictionaryDir)
            .GetAllWordsAsync(bench.Manifest.Language, ct).ConfigureAwait(false);

        var validator = new ClueValidatorFactory().GetFor(bench.Manifest.Language, semanticCheckEnabled: true);
        var runner = new RandomBaselineRunner(words, validator, seed);

        var createdAtUtc = DateTime.UtcNow;
        var draftManifest = new RunManifest(
            Kind: "manifest",
            RunId: string.Empty,
            Stage: "generate",
            CreatedAtUtc: createdAtUtc,
            BenchFile: args.Require("bench").Replace('\\', '/'),
            BenchHash: bench.Manifest.BenchHash,
            PromptFile: null,
            PromptVersion: null,
            GenerationMode: "RandomBaseline",
            ReasoningEnabled: false,
            Provider: "None",
            BaseUrl: string.Empty,
            ModelId: $"random-baseline-seed-{seed}",
            ModelSnapshotDate: null,
            ProviderModelListHash: null,
            Temperature: 0,
            TopP: null,
            MaxOutputTokens: null,
            MaxRetries: 0,
            Language: bench.Manifest.Language,
            HarnessVersion: RunFile.HarnessVersion,
            OperatorNotes: notes);

        var hash8 = RunFile.ComputeHash8(draftManifest);
        var runId = RunFile.BuildRunId(createdAtUtc, null, draftManifest.ModelId, hash8);
        var runPath = Path.Combine(outDirectory, $"{runId}.jsonl");

        RunFile.WriteManifest(runPath, draftManifest with { RunId = runId });

        foreach (var board in bench.Boards)
        {
            foreach (var direction in BoardGeometry.AllDirections)
            {
                ct.ThrowIfCancellationRequested();
                RunFile.AppendAttempt(runPath, runner.RunDirection(board, direction));
            }
        }

        Console.WriteLine($"plancher aléatoire écrit : {runPath}");
        Console.WriteLine($"  seed : {seed}");
        Console.WriteLine($"  porte à franchir après décodage : recovery ≤ 0,15");
        return 0;
    }
}
