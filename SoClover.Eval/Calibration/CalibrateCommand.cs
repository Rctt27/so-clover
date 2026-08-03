using System.Diagnostics;
using System.Globalization;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Cli;
using SoClover.Eval.Config;
using SoClover.Eval.Decoder;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Scoring;
using SoClover.Infrastructure.AI.Prompts;

namespace SoClover.Eval.Calibration;

/// <summary>
/// Le rapport <c>calibration.&lt;date&gt;-&lt;empreinte&gt;.json</c>. <b>Committé</b> : avec les
/// deux corpus humains, il est l'investissement irremplaçable du chantier.
/// </summary>
public sealed record CalibrationReport(
    string CalibrationId,
    DateTime CreatedAtUtc,
    string DecoderFingerprint,
    string ModelId,
    int? CluePromptVersion,
    int DecodesPerClue,
    double Epsilon,
    AgreementReport Agreement,
    double SaturationRecovery,
    double FloorRecovery,
    IReadOnlyList<Gate> Gates,
    bool AllGatesPassed,
    string Verdict,
    IReadOnlyList<string> Reasons,
    double IntraJudgeAgreement,
    int DuplicatePairCount,
    bool IntraJudgeBelowGate,
    bool Position1Suspect,
    bool AnchorSuspect,
    string? OperatorNotes);

/// <summary>
/// Verbe <c>calibrate</c> : re-décode les indices de <c>comparisons.dev.jsonl</c> avec le décodeur
/// <b>courant</b>, calcule accord et κ, lit les deux portes externes, rend un verdict unique.
/// <para>
/// Verbe <b>autonome</b> et non une jointure sur les <c>.decoded.jsonl</c> : les indices
/// <c>assisted</c> n'appartiennent à aucun run (<c>HumanRunExport.Build</c> ne projette que les
/// lignes <c>elicitation</c>), et joindre exigerait de vérifier que quatre runs ont été décodés
/// par le même décodeur. Le verbe autonome garantit en plus que les <b>deux options d'un couple
/// sont décodées à l'identique</b>.
/// </para>
/// </summary>
public static class CalibrateCommand
{
    /// <summary>
    /// Avec <c>half_rate = 0,656</c>, une masse de couples serait ex æquo à R̄ = 0,5 des deux côtés.
    /// Passer de 3 à 5 décodages fait passer la granularité de 1/6 à 1/10 et desserre le
    /// dénominateur de l'accord. Le défaut de <c>decode</c> reste 3, inchangé.
    /// </summary>
    public const int DefaultDecodesPerClue = 5;

    public static async Task<int> ExecuteAsync(Args args, CancellationToken ct)
    {
        var comparisonsPath = args.Get("comparisons")
                              ?? Path.Combine("eval", "human", "comparisons.dev.jsonl");
        var outDirectory = args.Get("out") ?? Path.Combine("eval", "human");
        var decodesPerClue = args.GetInt("decodes", DefaultDecodesPerClue);
        var epsilon = ParseEpsilon(args.Get("epsilon"));
        var force = args.Has("force");
        var notes = args.Get("notes");
        var modelSnapshotDate = args.Get("model-snapshot") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");

        var comparisons = HumanFile.ReadComparisons(comparisonsPath);
        var benchPath = args.Get("bench") ?? comparisons.Manifest.BenchFile;
        var bench = BenchFile.Read(benchPath);
        HumanFile.RequireBench(comparisonsPath, comparisons.Manifest.BenchHash, bench);

        var lot = CalibrationSet.Build(comparisons);
        if (lot.Couples.Count == 0)
            throw new InvalidOperationException(
                $"{comparisonsPath} ne porte aucun couple exploitable. Une porte franchie sur un " +
                "lot partiel n'est pas une porte.");

        // ---- Décodeur courant, et son empreinte ----------------------------
        var config = EvalLlmConfig.BuildConfiguration();
        var llmOptions = EvalLlmConfig.Bind(config, "Decoder");
        var opts = llmOptions.Value;

        using var chatClient = EvalLlmConfig.CreateChatClient(llmOptions);
        var loader = new FilePromptLoader();
        var cluePromptPath = Path.Combine(
            AppContext.BaseDirectory, "Decoder", "Prompts", "fr", "decode-clue.md");
        var decoder = new ClueDecoder(
            chatClient, loader, cluePromptPath, opts.DefaultModel,
            (float)opts.DefaultTemperature, (float?)opts.TopP, opts.MaxOutputTokens);

        var fingerprint = DecoderFingerprint.Compute(
            opts.DefaultModel, cluePromptPath, decoder.PromptVersion,
            opts.DefaultTemperature, opts.TopP, opts.MaxOutputTokens);

        // Les quatre portes doivent porter sur le MÊME décodeur — vérifié AVANT de dépenser
        // ~900 appels au LLM.
        var saturationMetrics = args.Require("saturation-metrics");
        var floorMetrics = args.Require("floor-metrics");
        CalibrationGates.RequireSameDecoder(fingerprint, saturationMetrics, floorMetrics);

        var calibrationId = $"{DateTime.UtcNow:yyyyMMdd}-{fingerprint}";
        var path = CalibrationFile.PathFor(outDirectory, calibrationId);
        var existing = force ? null : CalibrationFile.ReadOrNull(path);

        if (existing is null)
        {
            var providerModelListHash = await EvalLlmConfig
                .FetchProviderModelListHashAsync(opts, ct).ConfigureAwait(false);

            CalibrationFile.WriteManifest(path, new CalibrationManifest(
                Kind: "manifest",
                CalibrationId: calibrationId,
                CreatedAtUtc: DateTime.UtcNow,
                ComparisonsFile: comparisonsPath.Replace('\\', '/'),
                BenchFile: benchPath.Replace('\\', '/'),
                BenchHash: bench.Manifest.BenchHash,
                CoupleCount: lot.Couples.Count + lot.Anchors.Count,
                ClueCount: lot.Clues.Count,
                DecoderFingerprint: fingerprint,
                Provider: opts.Provider.ToString(),
                BaseUrl: opts.BaseUrl,
                ModelId: opts.DefaultModel,
                ModelSnapshotDate: modelSnapshotDate,
                ProviderModelListHash: providerModelListHash,
                Temperature: opts.DefaultTemperature,
                TopP: opts.TopP,
                MaxOutputTokens: opts.MaxOutputTokens,
                CluePromptFile: cluePromptPath.Replace('\\', '/'),
                CluePromptVersion: decoder.PromptVersion,
                DecodesPerClue: decodesPerClue,
                Epsilon: epsilon,
                HarnessVersion: CalibrationFile.HarnessVersion,
                OperatorNotes: notes));
        }
        else if (existing.Manifest.DecodesPerClue != decodesPerClue)
        {
            // Mélanger 3 et 5 décodages dans un même fichier produirait des R̄ de granularités
            // différentes selon l'indice. Même règle que decode, pour n'en avoir qu'une à retenir.
            throw new InvalidOperationException(
                $"{path} porte decodesPerClue={existing.Manifest.DecodesPerClue}, incompatible avec " +
                $"--decodes {decodesPerClue}. Utiliser --force pour repartir de zéro.");
        }
        else if (Math.Abs(existing.Manifest.Epsilon - epsilon) > 1e-12)
        {
            throw new InvalidOperationException(
                $"{path} porte epsilon={existing.Manifest.Epsilon}, incompatible avec --epsilon {epsilon}. " +
                "ε se décide AVANT de lire l'accord : le changer en cours de calibration est une " +
                "faute de protocole. Utiliser --force en connaissance de cause.");
        }

        var alreadyDecoded = existing is null
            ? []
            : existing.Decodes.Select(d => (d.BoardId, d.Direction, d.Clue, d.DecodeIndex)).ToHashSet();

        var boards = bench.Boards.ToDictionary(b => b.BoardId, StringComparer.Ordinal);

        Console.WriteLine($"calibration {calibrationId}");
        Console.WriteLine($"  fichier        : {path}");
        Console.WriteLine($"  comparaisons   : {comparisonsPath}");
        Console.WriteLine($"  couples        : {lot.Couples.Count} principaux + {lot.Anchors.Count} ancre(s)");
        Console.WriteLine($"  indices        : {lot.Clues.Count} distinct(s) × {decodesPerClue} décodages");
        Console.WriteLine($"  modèle         : {opts.DefaultModel} (snapshot {modelSnapshotDate})");
        Console.WriteLine($"  prompt         : clue v{decoder.PromptVersion}");
        Console.WriteLine($"  empreinte      : {fingerprint}");
        Console.WriteLine($"  ε              : {epsilon.ToString("0.###", CultureInfo.InvariantCulture)}");

        var stopwatch = Stopwatch.StartNew();
        var done = 0;

        foreach (var clue in lot.Clues)
        {
            ct.ThrowIfCancellationRequested();

            var board = boards[clue.BoardId];
            var direction = Enum.Parse<Direction>(clue.Direction);

            for (var index = 0; index < decodesPerClue; index++)
            {
                if (alreadyDecoded.Contains((clue.BoardId, clue.Direction, clue.Clue, index)))
                    continue;

                // ShuffleSeed.ForClue ne dépend NI de la direction NI de l'indice : les deux
                // options d'un couple voient le même ordre à decodeIndex égal. L'ordre de
                // présentation ne peut donc structurellement pas expliquer une préférence du
                // décodeur — le contrôle est apparié, gratuitement.
                var line = await decoder
                    .DecodeAsync(board, direction, clue.Clue, index, bench.Manifest.BenchHash, ct)
                    .ConfigureAwait(false);

                CalibrationFile.AppendDecode(path, CalibrationDecode.From(line, clue.Clue));
            }

            done++;
            if (done % 20 == 0)
                Console.WriteLine($"  {done}/{lot.Clues.Count} indices — écoulé {stopwatch.Elapsed:hh\\:mm\\:ss}");
        }

        // ---- Agrégation ----------------------------------------------------
        var decodes = CalibrationFile.Read(path);
        var rBar = CalibrationSet.RBarByClue(decodes);
        var agreement = AgreementMetrics.Compute(
            lot, rBar, epsilon,
            args.GetInt("bootstrap", Bootstrap.DefaultIterations),
            args.GetLong("seed", AgreementMetrics.DefaultBootstrapSeed));

        var saturationRecovery = ReadRecovery(saturationMetrics);
        var floorRecovery = ReadRecovery(floorMetrics);
        var verdict = CalibrationGates.Evaluate(agreement, saturationRecovery, floorRecovery);

        var humanReport = HumanReport.ForComparisons(comparisons);

        var report = new CalibrationReport(
            CalibrationId: calibrationId,
            CreatedAtUtc: DateTime.UtcNow,
            DecoderFingerprint: fingerprint,
            ModelId: opts.DefaultModel,
            CluePromptVersion: decoder.PromptVersion,
            DecodesPerClue: decodesPerClue,
            Epsilon: epsilon,
            Agreement: agreement,
            SaturationRecovery: saturationRecovery,
            FloorRecovery: floorRecovery,
            Gates: verdict.Gates,
            AllGatesPassed: verdict.AllPassed,
            Verdict: verdict.Label,
            Reasons: verdict.Reasons,
            IntraJudgeAgreement: humanReport.IntraJudgeAgreement,
            DuplicatePairCount: humanReport.DuplicatePairCount,
            IntraJudgeBelowGate: humanReport.DuplicatePairCount > 0
                                 && humanReport.IntraJudgeAgreement < CalibrationGates.MinAgreement,
            Position1Suspect: humanReport.Position1Suspect,
            AnchorSuspect: humanReport.AnchorSuspect,
            OperatorNotes: notes);

        var reportPath = CalibrationFile.ReportPathFor(path);
        File.WriteAllText(reportPath, EvalJson.Serialize(report));

        Print(report, stopwatch.Elapsed);
        Console.WriteLine($"rapport écrit : {reportPath}");
        return 0;
    }

    private static double ParseEpsilon(string? raw) =>
        raw is null
            ? AgreementMetrics.DefaultEpsilon
            : double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v
                : throw new ArgumentException($"--epsilon attend un décimal, valeur reçue : \"{raw}\"");

    private static double ReadRecovery(string metricsPath)
    {
        var metrics = EvalJson.Deserialize<MetricsReport>(File.ReadAllText(metricsPath));
        return metrics.Recovery;
    }

    private static void Print(CalibrationReport r, TimeSpan elapsed)
    {
        static string N(double v) => v.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"));

        var a = r.Agreement;

        Console.WriteLine();
        Console.WriteLine($"terminé en {elapsed:hh\\:mm\\:ss}");
        Console.WriteLine();
        Console.WriteLine("accord décodeur / humain");
        Console.WriteLine($"  accord (hors égalités) {N(a.Agreement)}   IC 95 % [{N(a.AgreementCiLow)} ; {N(a.AgreementCiHigh)}]");
        Console.WriteLine($"  dénominateur           {a.DecidedCount} / {a.CoupleCount} couples");
        Console.WriteLine($"  égalités humain        {N(a.HumanTieRate)}");
        Console.WriteLine($"  égalités décodeur      {N(a.DecoderTieRate)}");
        Console.WriteLine();
        Console.WriteLine("Cohen's κ");
        Console.WriteLine($"  κ global               {N(a.Kappa)}   IC 95 % [{N(a.KappaCiLow)} ; {N(a.KappaCiHigh)}]");
        Console.WriteLine($"  PABAK (diagnostic)     {N(a.Pabak)}   ← jamais une porte");
        foreach (var f in a.ByFamily)
            Console.WriteLine($"  κ {f.Family,-18} {N(f.Kappa)}   accord {N(f.Agreement)}   (n={f.DecidedCount})");
        Console.WriteLine();
        Console.WriteLine("table de contingence (humain × décodeur, sur les couples doublement tranchés)");
        foreach (var cell in a.Contingency)
            Console.WriteLine($"  humain {cell.HumanVerdict} × décodeur {cell.DecoderVerdict}   {cell.Count,4}");
        Console.WriteLine($"  marginales humain      A {N(a.HumanMarginalA)} / B {N(a.HumanMarginalB)}");
        Console.WriteLine($"  marginales décodeur    A {N(a.DecoderMarginalA)} / B {N(a.DecoderMarginalB)}");
        Console.WriteLine();
        Console.WriteLine($"ancres                   {a.AnchorCorrect} / {a.AnchorCount}   (hors calcul principal)");
        Console.WriteLine($"cohérence intra-juge     {N(r.IntraJudgeAgreement)}   (sur {r.DuplicatePairCount} doublon(s))");

        // Exiger du décodeur un accord supérieur à celui du juge avec lui-même n'a aucun sens.
        if (r.IntraJudgeBelowGate)
            Console.WriteLine(
                $"  ⚠ la cohérence intra-juge est sous {N(CalibrationGates.MinAgreement)} : la porte d'accord " +
                "est INATTEIGNABLE PAR CONSTRUCTION. C'est une information sur le corpus, pas sur le décodeur.");

        // Le PRD demande que le lot suspect soit consigné DANS LE REGISTRE.
        if (r.Position1Suspect)
            Console.WriteLine("  ⚠ LOT SUSPECT (position 1) — à consigner au registre avant d'en tirer une porte.");
        if (r.AnchorSuspect)
            Console.WriteLine("  ⚠ LOT SUSPECT (ancres ratées) — à consigner au registre avant d'en tirer une porte.");

        Console.WriteLine();
        Console.WriteLine("portes");
        foreach (var g in r.Gates)
            Console.WriteLine($"  {(g.Passed ? "✓" : "✗")} {g.Name,-16} {N(g.Value)}   ({g.Comparison} {g.Threshold:0.00})");
        Console.WriteLine();
        Console.WriteLine($"{r.Verdict}   empreinte {r.DecoderFingerprint}");
        foreach (var reason in r.Reasons)
            Console.WriteLine($"    — {reason}");

        if (!r.AllGatesPassed)
        {
            Console.WriteLine();
            Console.WriteLine(
                "  Retour en P3 : une variable à la fois — prompt decode-clue (version incrémentée), " +
                "puis modèle, puis température / maxOutputTokens, puis decodesPerClue. " +
                "INTERDIT : ajuster le décodeur en regardant les désaccords couple par couple — " +
                "on lit au plus une dizaine de désaccords pour DIAGNOSTIQUER, jamais pour ajuster.");
        }
        Console.WriteLine();
    }
}
