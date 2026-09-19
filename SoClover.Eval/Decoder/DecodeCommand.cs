using System.Diagnostics;
using SoClover.Eval.Bench;
using SoClover.Eval.Cli;
using SoClover.Eval.Calibration;
using SoClover.Eval.Config;
using SoClover.Eval.Io;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Prompts;
using SoClover.Eval.Scoring;
using SoClover.Eval.Tracing;
using SoClover.Infrastructure.AI.Prompts;

namespace SoClover.Eval.Decoder;

/// <summary>
/// Verbe <c>decode</c> : run d'indices → décodages N2 (3 par indice) et N3 (1 par board).
/// Deuxième passe du harnais, séparée de <c>generate</c> par un rechargement manuel de modèle
/// dans LM Studio.
/// </summary>
public static class DecodeCommand
{
    public const int DefaultDecodesPerClue = 3;

    public static async Task<int> ExecuteAsync(Args args, CancellationToken ct)
    {
        var runPath = args.Require("run");
        var decodesPerClue = args.GetInt("decodes", DefaultDecodesPerClue);
        var notes = args.Get("notes");
        var force = args.Has("force");
        var modelSnapshotDate = args.Get("model-snapshot") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");

        var run = RunFile.Read(runPath);
        var bench = BenchFile.Read(run.Manifest.BenchFile);

        if (bench.Manifest.BenchHash != run.Manifest.BenchHash)
            throw new InvalidOperationException(
                $"Le run déclare benchHash {run.Manifest.BenchHash}, le banc porte {bench.Manifest.BenchHash}. " +
                "Décoder un run contre un autre banc est une erreur de protocole.");

        var isHumanRun = run.Manifest.GenerationMode == SoClover.Eval.Human.HumanRunExport.GenerationModeName;

        var config = EvalLlmConfig.BuildConfiguration();
        var llmOptions = EvalLlmConfig.Bind(config, "Decoder");
        var opts = llmOptions.Value;
        var langfuseOptions = EvalLlmConfig.BindLangfuse(config);

        // Refus (banc de test, pseudo-run humain, clés absentes, Langfuse injoignable) AVANT tout
        // appel LLM : aucun repli silencieux sur --trace off.
        using var tracing = await TracingSetup
            .StartAsync(args, langfuseOptions, bench.Manifest, isHumanRun, ct).ConfigureAwait(false);
        var client = tracing.IsOn ? LangfuseClientFactory.CreateRequired(langfuseOptions) : null;
        // Sans traçage, aucune activité n'est créée : l'id de dataset n'est jamais lu.
        var datasetId = client is null
            ? string.Empty
            : (await LangfuseExportCommand.RequireDatasetAsync(client, bench, run.Manifest.BenchFile, ct).ConfigureAwait(false)).Id;

        using var chatClient = EvalLlmConfig.CreateChatClient(llmOptions);
        var loader = new FilePromptLoader();
        var selection = PromptSelectionArgs.From(args, langfuseOptions);
        var resolver = new PromptResolver(LangfuseClientFactory.CreateOrNull(langfuseOptions), PromptResolver.DefaultRoot);
        var cluePrompt = await resolver.ResolveAsync(SoCloverPrompt.DecoderFrClue, selection, ct).ConfigureAwait(false);
        var boardPrompt = await resolver.ResolveAsync(
            SoCloverPrompt.DecoderFrBoard, PromptSelectionArgs.ForBoardPrompt(selection, langfuseOptions), ct).ConfigureAwait(false);
        var cluePromptPath = cluePrompt.Path;
        var boardPromptPath = boardPrompt.Path;

        var clueDecoder = new ClueDecoder(
            chatClient, loader, cluePromptPath, opts.DefaultModel,
            (float)opts.DefaultTemperature, (float?)opts.TopP, opts.MaxOutputTokens);
        var boardDecoder = new BoardDecoder(
            chatClient, loader, boardPromptPath, opts.DefaultModel,
            (float)opts.DefaultTemperature, (float?)opts.TopP, opts.MaxOutputTokens);

        // L'empreinte détermine le CHEMIN : deux décodeurs sur le même run sont deux fichiers.
        // Un décodage écrasé, ce sont plusieurs centaines d'appels au LLM perdus sans un mot.
        var fingerprint = DecoderFingerprint.Compute(
            opts.DefaultModel, cluePromptPath, clueDecoder.PromptVersion,
            opts.DefaultTemperature, opts.TopP, opts.MaxOutputTokens);

        var decodedPath = DecodeFile.PathFor(runPath, fingerprint);
        var existing = force ? null : DecodeFile.ReadOrNull(decodedPath);

        DecodeManifest manifest;
        if (existing is null)
        {
            var providerModelListHash = await EvalLlmConfig
                .FetchProviderModelListHashAsync(opts, ct).ConfigureAwait(false);

            var runtime = await ModelRuntimeProbe
                .FetchAsync(opts, opts.DefaultModel, ct).ConfigureAwait(false);

            manifest = new DecodeManifest(
                Kind: "manifest",
                DecodeRunId: $"{run.Manifest.RunId}+decode-{DateTime.UtcNow:yyyyMMddHHmmss}",
                CreatedAtUtc: DateTime.UtcNow,
                GeneratorRunId: run.Manifest.RunId,
                BenchFile: run.Manifest.BenchFile,
                BenchHash: run.Manifest.BenchHash,
                Provider: opts.Provider.ToString(),
                BaseUrl: opts.BaseUrl,
                ModelId: opts.DefaultModel,
                ModelSnapshotDate: modelSnapshotDate,
                ProviderModelListHash: providerModelListHash,
                Temperature: opts.DefaultTemperature,
                TopP: opts.TopP,
                MaxOutputTokens: opts.MaxOutputTokens,
                CluePromptFile: cluePromptPath.Replace('\\', '/'),
                CluePromptVersion: clueDecoder.PromptVersion,
                BoardPromptFile: boardPromptPath.Replace('\\', '/'),
                BoardPromptVersion: boardDecoder.PromptVersion,
                DecodesPerClue: decodesPerClue,
                HarnessVersion: RunFile.HarnessVersion,
                OperatorNotes: notes,
                Quantization: runtime.Quantization,
                LoadedContextLength: runtime.LoadedContextLength,
                CluePrompt: cluePrompt.Provenance,
                BoardPrompt: boardPrompt.Provenance,
                Tracing: tracing.Manifest);
            DecodeFile.WriteManifest(decodedPath, manifest);
        }
        else
        {
            RequireCompatibleResume(existing.Manifest, fingerprint, decodesPerClue, decodedPath, cluePrompt.Provenance);
            TracingManifest.RequireSameMode(existing.Manifest.Tracing, tracing.Manifest, decodedPath);
            manifest = existing.Manifest;
        }

        var alreadyDecoded = existing is null
            ? []
            : existing.ClueDecodes.Select(d => (d.BoardId, d.Direction, d.DecodeIndex)).ToHashSet();
        var alreadyBoardDecoded = existing is null
            ? []
            : existing.BoardDecodes.Select(d => d.BoardId).ToHashSet();

        // Indice retenu par direction : la dernière tentative valide du run générateur.
        IReadOnlyDictionary<(string BoardId, string Direction), string> validClues = run.Attempts
            .Where(a => a.Valid && a.Clue is not null)
            .GroupBy(a => (a.BoardId, a.Direction))
            .ToDictionary(g => g.Key, g => g.Last().Clue!);

        Console.WriteLine($"décodage de {run.Manifest.RunId}");
        Console.WriteLine($"  fichier        : {decodedPath}");
        Console.WriteLine($"  modèle         : {opts.DefaultModel} (snapshot {modelSnapshotDate})");
        Console.WriteLine($"  prompts        : clue {cluePrompt.Describe()}, board {boardPrompt.Describe()}");
        Console.WriteLine($"  indices valides: {validClues.Count} / {bench.Manifest.BoardCount * 4}");
        Console.WriteLine($"  décodages/clue : {decodesPerClue}");

        var stopwatch = Stopwatch.StartNew();

        var experimentId = LangfuseExportCommand.ExperimentName(run.Manifest.RunId, fingerprint);
        var ctx = new DecodeUnitContext(run, manifest, experimentId, datasetId, fingerprint, decodesPerClue,
            bench.Manifest.BenchHash, validClues, alreadyDecoded, alreadyBoardDecoded);

        foreach (var board in DecodeResume.PendingBoards(bench, validClues, existing, decodesPerClue))
        {
            ct.ThrowIfCancellationRequested();

            var unit = await DecodeBoardUnit.RunAsync(clueDecoder, boardDecoder, ctx, board, ct).ConfigureAwait(false);

            // Traces, puis scores d'item (REST, idempotents), puis artefact : une panne à l'une des
            // deux premières étapes laisse le board non écrit, donc refait à la reprise (§8 bis).
            tracing.Checkpoint(board.BoardId);
            if (client is not null)
            {
                foreach (var score in ExperimentScores.ForItems(experimentId, unit.Items))
                    await client.CreateScoreAsync(score, ct).ConfigureAwait(false);
            }
            // Un seul append par board : pas de board à moitié écrit après un arrêt brutal.
            DecodeFile.AppendBoardUnit(decodedPath, unit.ClueLines, unit.BoardLine);

            Console.WriteLine($"  {board.BoardId} décodé — écoulé {stopwatch.Elapsed:hh\\:mm\\:ss}");
        }

        if (client is not null)
        {
            var decodedNow = DecodeFile.Read(decodedPath);
            var metrics = RunMetrics.Compute(bench, run, decodedNow, run.Manifest.MaxRetries + 1);
            var (from, to) = LangfuseExportCommand.SearchWindow(run.Manifest.CreatedAtUtc, decodedNow.Manifest.CreatedAtUtc);
            if (!await ExperimentRunScores.PublishAsync(client, experimentId, metrics, from, to, ct).ConfigureAwait(false))
                Console.Error.WriteLine(
                    "AVERTISSEMENT : experiment pas encore visible, scores de run non publiés — " +
                    "`score` puis `langfuse-export` les republieront (sans renvoyer de spans).");
            Console.WriteLine($"experiment Langfuse : {experimentId}");
        }

        Console.WriteLine($"terminé en {stopwatch.Elapsed:hh\\:mm\\:ss}. Décodage : {decodedPath}");
        return 0;
    }

    /// <summary>
    /// Reprendre un décodage n'est légitime que sous le <b>même décodeur</b> et le même nombre de
    /// décodages par indice.
    /// <para>
    /// La garde ne comparait que <c>decodesPerClue</c> : changer de modèle, de prompt ou de
    /// température entre deux passes ajoutait les nouvelles lignes sous un manifeste qui ne nomme
    /// que le premier décodeur, et <c>DecoderFingerprint.FromManifest</c> rendait dès lors une
    /// empreinte fausse pour la moitié du fichier. Rien, dans aucun chiffre publié, ne l'aurait
    /// signalé.
    /// </para>
    /// <para>
    /// Les deux vérifications restent <b>distinctes</b> : <c>decodesPerClue</c> est délibérément
    /// hors de l'empreinte (granularité de R̄, pas décodeur), donc l'une ne couvre pas l'autre.
    /// </para>
    /// <para>
    /// Garde 0 (spec §6.3) : l'empreinte hache le chemin canonique et la version <b>déclarée</b>,
    /// pas le contenu. Quand le manifeste existant et le prompt clue résolu portent tous deux une
    /// provenance, un <c>ContentSha256</c> différent refuse la reprise. Un manifeste antérieur à la
    /// provenance ne se juge pas sur ce point.
    /// </para>
    /// </summary>
    internal static void RequireCompatibleResume(
        DecodeManifest existing, string currentFingerprint, int decodesPerClue, string decodedPath,
        PromptProvenance? currentCluePrompt = null)
    {
        var existingFingerprint = DecoderFingerprint.FromManifest(existing);
        if (!string.Equals(existingFingerprint, currentFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"{decodedPath} a été produit par le décodeur {existingFingerprint} " +
                $"({existing.ModelId}, prompt v{existing.CluePromptVersion}, temp " +
                $"{existing.Temperature}), le décodeur courant est {currentFingerprint}. " +
                "Reprendre mélangerait deux décodeurs sous un manifeste qui n'en nomme qu'un. " +
                "Charger le décodeur d'origine, ou --force pour repartir de zéro.");

        if (existing.CluePrompt is { } existingClue && currentCluePrompt is { } currentClue
            && !string.Equals(existingClue.ContentSha256, currentClue.ContentSha256, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"{decodedPath} a été décodé avec un prompt clue de contenu sha {existingClue.ContentSha256[..8]}, " +
                $"le prompt résolu a le contenu sha {currentClue.ContentSha256[..8]} sous la même version " +
                $"déclarée v{existing.CluePromptVersion} (garde 0). Reprendre mélangerait deux contenus sous une " +
                "même empreinte. Bumper le frontmatter du prompt, résoudre la version d'origine, ou --force " +
                "pour repartir de zéro.");

        if (existing.DecodesPerClue != decodesPerClue)
            throw new InvalidOperationException(
                $"{decodedPath} porte decodesPerClue={existing.DecodesPerClue}, " +
                $"incompatible avec --decodes {decodesPerClue}. Utiliser --force pour repartir de zéro.");
    }
}
