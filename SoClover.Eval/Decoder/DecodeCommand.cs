using System.Diagnostics;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Cli;
using SoClover.Eval.Calibration;
using SoClover.Eval.Config;
using SoClover.Eval.Io;
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

        var config = EvalLlmConfig.BuildConfiguration();
        var llmOptions = EvalLlmConfig.Bind(config, "Decoder");
        var opts = llmOptions.Value;

        using var chatClient = EvalLlmConfig.CreateChatClient(llmOptions);
        var loader = new FilePromptLoader();
        var cluePromptPath = Path.Combine(
            AppContext.BaseDirectory, "Decoder", "Prompts", "fr", "decode-clue.md");
        var boardPromptPath = Path.Combine(
            AppContext.BaseDirectory, "Decoder", "Prompts", "fr", "decode-board.md");

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

        if (existing is null)
        {
            var providerModelListHash = await EvalLlmConfig
                .FetchProviderModelListHashAsync(opts, ct).ConfigureAwait(false);

            DecodeFile.WriteManifest(decodedPath, new DecodeManifest(
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
                OperatorNotes: notes));
        }
        else if (existing.Manifest.DecodesPerClue != decodesPerClue)
        {
            throw new InvalidOperationException(
                $"{decodedPath} porte decodesPerClue={existing.Manifest.DecodesPerClue}, " +
                $"incompatible avec --decodes {decodesPerClue}. Utiliser --force pour repartir de zéro.");
        }

        var alreadyDecoded = existing is null
            ? []
            : existing.ClueDecodes.Select(d => (d.BoardId, d.Direction, d.DecodeIndex)).ToHashSet();
        var alreadyBoardDecoded = existing is null
            ? []
            : existing.BoardDecodes.Select(d => d.BoardId).ToHashSet();

        // Indice retenu par direction : la dernière tentative valide du run générateur.
        var validClues = run.Attempts
            .Where(a => a.Valid && a.Clue is not null)
            .GroupBy(a => (a.BoardId, a.Direction))
            .ToDictionary(g => g.Key, g => g.Last().Clue!);

        Console.WriteLine($"décodage de {run.Manifest.RunId}");
        Console.WriteLine($"  fichier        : {decodedPath}");
        Console.WriteLine($"  modèle         : {opts.DefaultModel} (snapshot {modelSnapshotDate})");
        Console.WriteLine($"  prompts        : clue v{clueDecoder.PromptVersion}, board v{boardDecoder.PromptVersion}");
        Console.WriteLine($"  indices valides: {validClues.Count} / {bench.Manifest.BoardCount * 4}");
        Console.WriteLine($"  décodages/clue : {decodesPerClue}");

        var stopwatch = Stopwatch.StartNew();

        foreach (var board in bench.Boards)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var direction in BoardGeometry.AllDirections)
            {
                if (!validClues.TryGetValue((board.BoardId, direction.ToString()), out var clue))
                    continue; // Aucun indice valide : la direction comptera R̄ = 0 au scoring.

                for (var index = 0; index < decodesPerClue; index++)
                {
                    if (alreadyDecoded.Contains((board.BoardId, direction.ToString(), index)))
                        continue;

                    var line = await clueDecoder
                        .DecodeAsync(board, direction, clue, index, bench.Manifest.BenchHash, ct)
                        .ConfigureAwait(false);
                    DecodeFile.AppendClueDecode(decodedPath, line);
                }
            }

            // N3 : seulement si les 4 directions ont un indice valide — une affectation
            // partielle ne mesure pas la cohérence board.
            var boardClues = BoardGeometry.AllDirections
                .Where(d => validClues.ContainsKey((board.BoardId, d.ToString())))
                .ToDictionary(d => d, d => validClues[(board.BoardId, d.ToString())]);

            if (boardClues.Count == 4 && !alreadyBoardDecoded.Contains(board.BoardId))
            {
                var line = await boardDecoder
                    .DecodeAsync(board, boardClues, bench.Manifest.BenchHash, ct)
                    .ConfigureAwait(false);
                DecodeFile.AppendBoardDecode(decodedPath, line);
            }

            Console.WriteLine($"  {board.BoardId} décodé — écoulé {stopwatch.Elapsed:hh\\:mm\\:ss}");
        }

        Console.WriteLine($"terminé en {stopwatch.Elapsed:hh\\:mm\\:ss}. Décodage : {decodedPath}");
        return 0;
    }
}
