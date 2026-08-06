using System.Globalization;
using SoClover.Domain;
using SoClover.Eval.Calibration;
using SoClover.Eval.Cli;
using SoClover.Eval.Decoder;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;

namespace SoClover.Eval.Scoring;

/// <summary>Verbe <c>score</c> : aucun appel LLM, uniquement de la lecture de fichiers.</summary>
public static class ScoreCommand
{
    public static Task<int> ExecuteAsync(Args args, CancellationToken ct)
    {
        var runPath = args.Require("run");
        var run = RunFile.Read(runPath);
        var bench = BenchFile.Read(run.Manifest.BenchFile);
        var decodedPath = args.Get("decoded") ?? DecodeFile.FindForRun(runPath);
        var decoded = decodedPath is null ? null : DecodeFile.ReadOrNull(decodedPath);

        var maxAttempts = run.Manifest.MaxRetries + 1;

        // Le banc entier reste le dénominateur par défaut. --subset ne s'invoque que pour un run
        // qui ne prétend pas le couvrir — le pseudo-run humain, aujourd'hui.
        IReadOnlySet<(string BoardId, string Direction)>? subset = null;
        string? subsetName = null;
        string? subsetOutcome = null;
        if (args.Get("subset") is { } subsetPath)
            (subset, subsetName, subsetOutcome) =
                SubsetSelector.FromFile(subsetPath, args.Get("subset-outcome"));

        // La provenance part dans le .metrics.json : ce fichier vit à un chemin fixe par run ET
        // PAR DÉCODEUR, et chaque `score` sous le même décodeur écrase le précédent. Sans elle,
        // `calibrate --saturation-metrics` ne peut pas vérifier que le fichier désigné porte bien
        // sur les seuls indices `solide`.
        var metrics = RunMetrics.Compute(
            bench, run, decoded, maxAttempts, subset, subsetName, subsetOutcome);
        var benchDirectionCount = bench.Manifest.BoardCount * BoardGeometry.AllDirections.Count;

        Print(metrics, run.Manifest.OperatorNotes, decoded?.Manifest.ModelId, subsetName, benchDirectionCount);

        // PerItemRBar est [JsonIgnore] : le détail par item se recalcule depuis les fichiers
        // de run, et n'a pas à figurer dans l'artefact de synthèse.
        // Frère du décodage employé, jamais du run seul : c'est par cette fratrie que
        // CalibrationGates.FingerprintOfMetrics retrouve quel décodeur a produit ces chiffres.
        // Un run sans décodage n'a pas de décodeur — le chemin retombe alors sur le run.
        var metricsPath = decodedPath is null
            ? Path.ChangeExtension(runPath, ".metrics.json")
            : DecodeFile.MetricsPathFor(decodedPath);
        File.WriteAllText(metricsPath, EvalJson.Serialize(metrics));
        Console.WriteLine($"métriques écrites : {metricsPath}");

        if (args.Get("ledger") is { } ledgerPath)
        {
            var settings = ComposeSettings(
                run.Manifest, subsetName, metrics.DirectionCount, benchDirectionCount);

            var calibrationPath = args.Get("calibration");
            var calibration = calibrationPath is null
                ? null
                : EvalJson.Deserialize<CalibrationReport>(File.ReadAllText(calibrationPath));

            LedgerWriter.Append(ledgerPath, new LedgerEntry(
                Date: DateOnly.FromDateTime(DateTime.UtcNow),
                RunId: run.Manifest.RunId,
                BenchFile: run.Manifest.BenchFile,
                PromptFile: run.Manifest.PromptFile,
                PromptVersion: run.Manifest.PromptVersion,
                ModelId: run.Manifest.ModelId,
                ModelSnapshotDate: run.Manifest.ModelSnapshotDate,
                Settings: settings,
                Metrics: metrics,
                Status: ResolveStatus(calibration, decoded, calibrationPath ?? "—"),
                Hypothesis: args.Get("hypothesis"),
                Decision: args.Get("decision") ?? "neutre",
                OperatorNotes: ComposeNotes(run.Manifest.OperatorNotes, decoded)));

            Console.WriteLine($"ligne ajoutée au registre : {ledgerPath}");
        }

        return Task.FromResult(0);
    }

    /// <summary>
    /// Cellule <i>réglages</i> du registre. Quand le score porte sur un sous-ensemble, la mention
    /// <c>subset=&lt;nom&gt; (40/160)</c> s'y ajoute : aucune colonne nouvelle — les 18 colonnes du
    /// registre sont préservées — et surtout <b>aucune ligne ne peut prétendre porter sur le banc
    /// entier alors qu'elle porte sur un quart</b>.
    /// </summary>
    internal static string ComposeSettings(
        RunManifest manifest, string? subsetName, int subsetDirectionCount, int benchDirectionCount)
    {
        var settings = string.Create(CultureInfo.InvariantCulture,
            $"temp {manifest.Temperature} / topP {manifest.TopP?.ToString() ?? "—"} / " +
            $"maxTokens {manifest.MaxOutputTokens?.ToString() ?? "—"} / " +
            $"maxRetries {manifest.MaxRetries} / reasoning {manifest.ReasoningEnabled}");

        return subsetName is null
            ? settings
            : $"{settings} / subset={subsetName} ({subsetDirectionCount}/{benchDirectionCount})";
    }

    /// <summary>
    /// Le statut passe à <c>calibré</c> <b>si et seulement si</b> le fichier de calibration
    /// déclare les quatre portes franchies <b>et</b> que son empreinte est celle du
    /// <c>.decoded.jsonl</c> du run scoré.
    /// <para>
    /// Sinon : <b>refus bruyant</b>, jamais de dégradation silencieuse en <c>pré-calibration</c>.
    /// Qui passe le drapeau veut publier une ligne défendable ; produire à la place une ligne mal
    /// étiquetée serait le pire des deux mondes. <b>Sans</b> le drapeau, le comportement est
    /// inchangé.
    /// </para>
    /// </summary>
    internal static string ResolveStatus(
        CalibrationReport? calibration, DecodeContents? decoded, string calibrationPathForMessages)
    {
        if (calibration is null)
            return LedgerWriter.PreCalibrationStatus;

        if (!calibration.AllGatesPassed)
            throw new InvalidOperationException(
                $"{calibrationPathForMessages} déclare « {calibration.Verdict} » : toutes les portes " +
                "ne sont pas franchies, aucune ligne ne peut être publiée comme défendable. " +
                "Retirer --calibration, ou refranchir les portes.");

        if (decoded is null)
            throw new InvalidOperationException(
                "Ce run n'a pas de fichier de décodage : un statut calibré suppose un recovery, " +
                "donc un décodage. Lancer `decode` avant de scorer avec --calibration.");

        var runFingerprint = DecoderFingerprint.FromManifest(decoded.Manifest);
        if (!string.Equals(runFingerprint, calibration.DecoderFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Le run a été décodé par l'empreinte {runFingerprint}, la calibration porte sur " +
                $"{calibration.DecoderFingerprint}. Deux recovery d'empreintes différentes ne se " +
                "comparent pas — re-décoder ce run avec le décodeur calibré (decode --force).");

        return LedgerWriter.CalibratedStatusFor(calibration.DecoderFingerprint);
    }

    /// <summary>
    /// Fusionne les notes du run générateur et celles du décodage en une seule colonne de
    /// registre, en attribuant chaque moitié.
    /// <para>
    /// Le <c>recovery</c> d'une ligne n'est interprétable que si l'on sait <b>quel décodeur</b>
    /// l'a produit : deux lignes décodées par des modèles différents ne se comparent pas. Le
    /// modèle décodeur est donc toujours nommé dès qu'un décodage existe, même sans aucune note.
    /// C'est aussi le seul endroit du registre où le toggle « enable thinking » de LM Studio —
    /// que rien n'observe automatiquement — peut apparaître.
    /// </para>
    /// </summary>
    internal static string? ComposeNotes(string? runNotes, DecodeContents? decoded)
    {
        if (decoded is null)
            return runNotes;

        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(runNotes))
            parts.Add($"gén. : {runNotes}");

        // Le pipe est le séparateur de colonnes du registre ; LedgerWriter le neutraliserait
        // en « ¦ ». Un séparateur qui n'en est pas un garde la fusion lisible.
        var decoder = $"déc. : {decoded.Manifest.ModelId}";
        if (!string.IsNullOrWhiteSpace(decoded.Manifest.OperatorNotes))
            decoder += $" ({decoded.Manifest.OperatorNotes})";
        parts.Add(decoder);

        return string.Join(" ; ", parts);
    }

    /// <summary>
    /// Un taux suivi de ses effectifs. Sans eux, <c>0,000</c> se lit comme un fait alors qu'il
    /// peut n'être qu'un plancher d'estimateur : sur 22 items, <c>strict_2of2</c> ne peut valoir
    /// que 0 ; 0,045 ; 0,091… et un zéro y est le résultat attendu.
    /// <para>
    /// Dénominateur nul : on n'imprime pas de taux. <c>0,000</c> sur zéro item serait un chiffre
    /// entièrement fabriqué.
    /// </para>
    /// </summary>
    internal static string FormatRate(double value, int numerator, int denominator)
    {
        var counts = $"({numerator}/{denominator})";
        return denominator == 0
            ? $"  —       {counts}"
            : $"{value.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"))}   {counts}";
    }

    private static void Print(
        MetricsReport m, string? operatorNotes, string? decoderModel,
        string? subsetName, int benchDirectionCount)
    {
        static string N(double v) => v.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"));

        Console.WriteLine();
        Console.WriteLine($"run   : {m.RunId}");
        Console.WriteLine($"banc  : {m.BenchFile} ({m.BoardCount} boards, {m.DirectionCount} directions, hash {m.BenchHash})");
        if (subsetName is not null)
            Console.WriteLine(
                $"subset : {subsetName} — {m.DirectionCount}/{benchDirectionCount} directions. " +
                "Tous les dénominateurs ci-dessous portent sur ce sous-ensemble.");
        if (decoderModel is not null) Console.WriteLine($"décodeur : {decoderModel}");
        if (operatorNotes is not null) Console.WriteLine($"notes : {operatorNotes}");
        Console.WriteLine();
        var c = m.Counts;
        Console.WriteLine("N1 — validité");
        Console.WriteLine($"  valid_rate             {FormatRate(m.ValidRate, c.ValidItems, m.DirectionCount)}");
        Console.WriteLine($"  first_attempt_rate     {FormatRate(m.FirstAttemptRate, c.FirstAttemptItems, m.DirectionCount)}");
        Console.WriteLine($"  parse_failure_rate     {FormatRate(m.ParseFailureRate, c.ParseFailures, c.Attempts)}");
        Console.WriteLine();
        Console.WriteLine("N2 — devinabilité");
        Console.WriteLine($"  recovery               {N(m.Recovery)}   sur {m.DirectionCount} direction(s)   ← métrique principale");
        Console.WriteLine($"  strict_2of2_all_decodes {FormatRate(m.Strict2Of2, c.StrictItems, c.DecodedItems)}   ← unanimité des 3 décodages");
        Console.WriteLine($"  half_rate              {FormatRate(m.HalfRate, c.HalfItems, c.DecodedItems)}");
        Console.WriteLine();
        Console.WriteLine("N3 — cohérence board");
        Console.WriteLine($"  board_positions        {N(m.BoardPositions)}   sur {c.ScoredBoards} board(s)   ← pilotage N3");
        Console.WriteLine($"  board_solved_first_try {FormatRate(m.BoardSolvedFirstTry, c.SolvedBoards, c.ScoredBoards)}   ← témoin, pas un critère");
        Console.WriteLine();
        Console.WriteLine("santé");
        Console.WriteLine($"  decode_failure_rate    {FormatRate(m.DecodeFailureRate, c.DecodeFailures, c.Decodes)}");
        Console.WriteLine($"  items                  {m.ItemsCompleted} / {m.ItemsExpected}");

        if (m.ItemsCompleted < m.ItemsExpected)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  ⚠ RUN PARTIEL : {m.ItemsCompleted}/{m.ItemsExpected} directions terminées. " +
                "Un chiffre calculé sur un banc incomplet doit être visiblement suspect, pas publié.");
        }

        if (m.DecodeFailureRate > 0.05)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  ⚠ decode_failure_rate = {N(m.DecodeFailureRate)} > 0,05 : le prompt décodeur est cassé, " +
                "aucun recovery n'est lisible.");
        }

        if (m.ConfusionTop.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("confusion_top (mots non-référence les plus choisis)");
            foreach (var entry in m.ConfusionTop)
                Console.WriteLine($"  {entry.Count,4}  {entry.Word}");
        }
        Console.WriteLine();
    }
}
