using System.Globalization;
using SoClover.Domain;
using SoClover.Eval.Analysis;
using SoClover.Eval.Bench;
using SoClover.Eval.Calibration;
using SoClover.Eval.Cli;
using SoClover.Eval.Decoder;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using SoClover.Eval.Scoring;
using SoClover.Eval.Web;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Infrastructure.Validation;
using Microsoft.Extensions.Hosting;

namespace SoClover.Eval;

// Classe nommée (et non des top-level statements) : SoClover/Program.cs génère déjà une classe
// `Program` interne dans le namespace global, utilisée non-qualifiée par
// WebApplicationFactory<Program> dans PublicConfigHttpTests.cs et GetGameStateHttpRotationTests.cs.
// Des top-level statements ici généreraient une SECONDE classe `Program` dans le namespace global
// (celui de SoClover.Eval) et, comme SoClover.csproj accorde déjà InternalsVisibleTo à
// SoClover.Tests, accorder le même depuis SoClover.Eval rendrait `Program` ambigu (CS0433) dans ces
// tests. Une classe nommée dans un namespace dédié évite la collision définitivement.
internal static class EvalProgram
{
    internal static async Task<int> Main(string[] args)
    {
        var cliArgs = Args.Parse(args);

        try
        {
            var exitCode = cliArgs.Verb switch
            {
                "doctor" => Task.FromResult(Doctor()),
                "bench" => Bench(cliArgs),
                "generate" => GenerateCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "decode" => DecodeCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "score" => ScoreCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "compare" => CompareCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "calibrate" => CalibrateCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "analyze" => AnalyzeCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "elicit" => Elicit(cliArgs, CancellationToken.None),
                "judge" => Judge(cliArgs, CancellationToken.None),
                "human-report" => Task.FromResult(HumanReportCommand(cliArgs)),
                "human-run" => Task.FromResult(HumanRunCommand(cliArgs)),
                "" => Task.FromResult(Usage()),
                _ => Task.FromResult(Usage($"Verbe inconnu : {cliArgs.Verb}")),
            };
            return await exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERREUR : {ex.Message}");
            return 1;
        }
    }

    private static int Usage(string? message = null)
    {
        if (message is not null) Console.Error.WriteLine(message);
        Console.Error.WriteLine("""
            Usage : dotnet run --project SoClover.Eval -- <verbe> [options]

            Verbes :
              doctor    Vérifie que les prompts de SoClover sont résolvables depuis cet exécutable
              bench     Génère un banc seedé et l'écrit en JSONL
              generate  Banc -> indices (appelle le LLM générateur ; reprenable)
              decode    Run d'indices -> décodages N2/N3 (appelle le LLM décodeur ; reprenable)
              score     Calcule les 9 indicateurs N1-N3 + 2 de santé (aucun appel LLM)
              compare   Δ recovery apparié + IC bootstrap + verdict de promotion (aucun appel LLM)
              elicit    Séance A (auteur) : serveur local de saisie chronométrée
              judge     Séance B (juge) : serveur local de comparaison en aveugle, J+1
              human-run     Projette la séance A en pseudo-run décodable (aucun appel LLM)
              human-report  Agrégats des deux séances humaines (aucun appel LLM)
              calibrate     P6 : accord decodeur/humain, kappa, quatre portes, verdict unique
              analyze       P7 : taxonomie chiffree des modes d'echec (aucun appel LLM)

            Sous-ensemble (score, compare) :
              --subset <elicitation.jsonl>      restreint TOUS les dénominateurs aux directions
                                                réellement couvertes par la séance A
              --subset-outcome solide,tiede     « plafond sur paires résolues » (A-3) ; sans le
                                                drapeau, toutes les issues — c'est le plafond joué

            Registre (score) :
              --calibration <calibration.json>  statut `calibré` — refuse si une porte est tombée
                                                ou si l'empreinte du décodeur ne concorde pas

            Calibration (calibrate) :
              --comparisons <comparisons.jsonl>  corpus de la séance B (défaut eval/human/)
              --decodes 5                        granularité de R̄ ; le défaut de `decode` reste 3
              --saturation-metrics <x.metrics.json>  porte de non-saturation (recovery ≤ 0,95)
              --floor-metrics      <x.metrics.json>  porte du plancher       (recovery ≤ 0,15)
              --epsilon 0                        marge du verdict décodeur ; se décide AVANT de
                                                 lire l'accord, et part au manifeste

            Taxonomie (analyze) :
              --run <run.jsonl>                 run DÉCODÉ ; refuse sinon
              --sample 20 --seed S              échantillon seedé d'échecs, lisible à la main
              --review <run.sample.md>          matrice de confusion auto ↔ humain
            """);
        return message is null ? 0 : 2;
    }

    // Vérifie que les .md de SoClover (déclarés Content/CopyToOutputDirectory=Always) sont bien
    // propagés dans l'output de SoClover.Eval par la ProjectReference. Toute la suite du harnais
    // en dépend : sans ça, FrenchAiCluePromptProvider lève FileNotFoundException à l'exécution.
    private static int Doctor()
    {
        Console.WriteLine($"BaseDirectory : {AppContext.BaseDirectory}");

        var provider = new FrenchAiCluePromptProvider();
        var cards = new[]
        {
            new BoardCardSnapshot(BoardPosition.TopLeft,     "Lune",    "Route",  "Plage",    "Ciel"),
            new BoardCardSnapshot(BoardPosition.TopRight,    "Vague",   "Rocher", "Sable",    "Île"),
            new BoardCardSnapshot(BoardPosition.BottomRight, "Oiseau",  "Forêt",  "Montagne", "Vent"),
            new BoardCardSnapshot(BoardPosition.BottomLeft,  "Rivière", "Pont",   "Ville",    "Village"),
        };
        var context = new BoardCluesPromptContext(
            Language: "Français_OFF",
            Cards: cards,
            RemainingDirections: [Direction.Top],
            RejectedPerDirection: new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>());

        var bundle = provider.BuildSingleDirectionCluePrompt(context);

        Console.WriteLine($"prompt PerDirection FR : version {bundle.PromptVersion}");
        Console.WriteLine($"system : {bundle.SystemPrompt.Length} caractères");
        Console.WriteLine($"user   : {bundle.UserPrompt.Length} caractères");
        Console.WriteLine("OK — les prompts sont résolvables depuis SoClover.Eval.");
        return 0;
    }

    // Génère un banc. Les deux bancs officiels sont produits une fois puis committés :
    //   bench --id dev  --seed 20260726001 --boards 40 --out eval/boards.dev.jsonl
    //   bench --id test --seed 20260726002 --boards 60 --out eval/boards.test.jsonl
    private static async Task<int> Bench(Args args)
    {
        var benchId = args.Require("id");
        var seed = args.GetLong("seed", 0);
        var boardCount = args.GetInt("boards", 0);
        var outPath = args.Require("out");
        var language = args.Get("language") ?? "Français_OFF";

        if (seed == 0) throw new ArgumentException("--seed est requis et doit être non nul.");
        if (boardCount <= 0) throw new ArgumentException("--boards est requis et doit être > 0.");

        if (File.Exists(outPath) && !args.Has("force"))
            throw new InvalidOperationException(
                $"{outPath} existe déjà. Un banc committé ne se régénère pas : changer de nom de fichier, " +
                "ou passer --force en connaissance de cause.");

        var dictionaryDir = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Dictionaries");
        var dictionary = new FileWordDictionary(dictionaryDir);
        var words = await dictionary.GetAllWordsAsync(language);

        var dictionaryFile = Directory
            .EnumerateFiles(dictionaryDir, "*.txt")
            .Single(f => Path.GetFileNameWithoutExtension(f) == language);

        var contents = BenchGenerator.Generate(
            benchId, seed, boardCount, words, language,
            Path.GetFileName(dictionaryFile),
            EvalJson.DictionaryHash(words),
            DateTime.UtcNow);

        BenchFile.Write(outPath, contents);

        Console.WriteLine($"banc écrit : {outPath}");
        Console.WriteLine($"  benchId       : {contents.Manifest.BenchId}");
        Console.WriteLine($"  seed          : {contents.Manifest.Seed}");
        Console.WriteLine($"  boards        : {contents.Manifest.BoardCount} ({contents.Manifest.BoardCount * 4} directions)");
        Console.WriteLine($"  dictionnaire  : {contents.Manifest.DictionaryFile} ({words.Count} mots, sha {contents.Manifest.DictionaryHash})");
        Console.WriteLine($"  prng          : {contents.Manifest.PrngAlgorithm}");
        Console.WriteLine($"  benchHash     : {contents.Manifest.BenchHash}");
        return 0;
    }

    /// <summary>
    /// Verbe <c>elicit</c> : séance A. Ouvre un serveur local et rend la main quand l'opérateur
    /// arrête le processus. Aucun appel LLM — le run de candidats est lu sur disque.
    /// </summary>
    private static async Task<int> Elicit(Args args, CancellationToken ct)
    {
        var benchPath = args.Require("bench");
        var outPath = args.Get("out") ?? Path.Combine("eval", "human", "elicitation.dev.jsonl");
        var candidatesRunPath = args.Get("candidates-run");
        var pauseSeconds = args.GetInt("pause-seconds", 300);
        var port = args.GetInt("port", 5177);

        var bench = BenchFile.Read(benchPath);

        RunContents? candidatesRun = null;
        if (candidatesRunPath is not null)
        {
            candidatesRun = RunFile.Read(candidatesRunPath);
            if (candidatesRun.Manifest.BenchHash != bench.Manifest.BenchHash)
                throw new InvalidOperationException(
                    $"Le run de candidats porte benchHash {candidatesRun.Manifest.BenchHash}, " +
                    $"le banc {bench.Manifest.BenchHash}. Révéler les candidats d'un autre banc " +
                    "montrerait des indices sans rapport avec la paire cible.");
        }

        var existing = HumanFile.ReadElicitationOrNull(outPath);
        ElicitationManifest manifest;

        if (existing is null)
        {
            var seed = args.GetLong("seed", 0);
            if (seed == 0) throw new ArgumentException("--seed est requis et doit être non nul.");

            manifest = new ElicitationManifest(
                Kind: "manifest",
                BenchFile: benchPath.Replace('\\', '/'),
                BenchHash: bench.Manifest.BenchHash,
                Seed: seed,
                TargetCount: args.GetInt("count", 40),
                TimerSeconds: args.GetInt("timer", 90),
                QuotaBeforePause: args.GetInt("quota", 25),
                CandidatesRunId: candidatesRun?.Manifest.RunId,
                HarnessVersion: HumanFile.HarnessVersion,
                CreatedAtUtc: DateTime.UtcNow);

            HumanFile.WriteElicitationManifest(outPath, manifest);
            existing = HumanFile.ReadElicitation(outPath);
        }
        else
        {
            // Le manifeste fait foi à la reprise : réécrire la ligne 1 violerait l'append-only,
            // et un plan reconstruit avec un autre seed ne correspondrait plus aux lignes déjà
            // saisies. Pour étendre une séance, changer de nom de fichier.
            manifest = existing.Manifest;
            HumanFile.RequireBench(outPath, manifest.BenchHash, bench);

            if (args.Has("seed") && args.GetLong("seed", 0) != manifest.Seed)
                Console.Error.WriteLine(
                    $"AVERTISSEMENT : --seed ignoré, le manifeste de {outPath} porte {manifest.Seed}.");
        }

        var plan = ElicitationPlan.Build(bench, manifest.Seed, manifest.TargetCount);
        var validator = new ClueValidatorFactory().GetFor(bench.Manifest.Language, semanticCheckEnabled: true);

        var session = new ElicitationSession(
            bench, plan, existing,
            ElicitationSession.BuildCandidateIndex(candidatesRun),
            validator,
            outPath,
            sessionId: $"s-{DateTime.UtcNow:yyyyMMddHHmmss}",
            timerSeconds: manifest.TimerSeconds,
            quotaBeforePause: manifest.QuotaBeforePause,
            pauseSeconds: pauseSeconds);

        var pagePath = Path.Combine(AppContext.BaseDirectory, "Web", "Pages", "elicit.html");
        var app = HumanServer.BuildElicitApp(session, pagePath, port);
        await app.StartAsync(ct);

        Console.WriteLine($"séance A : {HumanServer.ResolveUrl(app)}");
        Console.WriteLine($"  banc          : {benchPath} (hash {bench.Manifest.BenchHash})");
        Console.WriteLine($"  fichier       : {outPath}");
        Console.WriteLine($"  plan          : {plan.Count} direction(s), seed {manifest.Seed}");
        Console.WriteLine($"  déjà saisies  : {session.CompletedCount}");
        Console.WriteLine($"  candidats     : {manifest.CandidatesRunId ?? "— (aucun run fourni)"}");
        Console.WriteLine("  Ctrl+C pour arrêter. La reprise est sûre : chaque item est écrit à la soumission.");

        await app.WaitForShutdownAsync(ct);
        return 0;
    }

    /// <summary>
    /// Verbe <c>judge</c> : séance B. Refuse de démarrer avant J+1 ; <c>--force-early</c> autorise
    /// l'entorse et la stampe dans le manifeste — l'entorse devient une donnée du corpus, pas un
    /// secret. Aucun appel LLM.
    /// </summary>
    private static async Task<int> Judge(Args args, CancellationToken ct)
    {
        var benchPath = args.Require("bench");
        var elicitationPath = args.Get("elicitation") ?? Path.Combine("eval", "human", "elicitation.dev.jsonl");
        var outPath = args.Get("out") ?? Path.Combine("eval", "human", "comparisons.dev.jsonl");
        var runAPath = args.Require("run-a");
        var runBPath = args.Require("run-b");
        var anchorPath = args.Get("anchor-run");
        var pauseSeconds = args.GetInt("pause-seconds", 300);
        var port = args.GetInt("port", 5178);

        var bench = BenchFile.Read(benchPath);
        var elicitation = HumanFile.ReadElicitation(elicitationPath);
        HumanFile.RequireBench(elicitationPath, elicitation.Manifest.BenchHash, bench);

        var runA = RunFile.Read(runAPath);
        var runB = RunFile.Read(runBPath);
        var anchorRun = anchorPath is null ? null : RunFile.Read(anchorPath);

        var sourceRuns = new List<(string Path, RunContents Run)> { (runAPath, runA), (runBPath, runB) };
        if (anchorPath is not null && anchorRun is not null)
            sourceRuns.Add((anchorPath, anchorRun));

        foreach (var (path, run) in sourceRuns)
        {
            if (run.Manifest.BenchHash != bench.Manifest.BenchHash)
                throw new InvalidOperationException(
                    $"{path} porte benchHash {run.Manifest.BenchHash}, incompatible avec {bench.Manifest.BenchHash}.");
        }

        var existing = HumanFile.ReadComparisonsOrNull(outPath);
        ComparisonManifest manifest;

        if (existing is null)
        {
            var seed = args.GetLong("seed", 0);
            if (seed == 0) throw new ArgumentException("--seed est requis et doit être non nul.");

            var guard = JudgeSession.Evaluate(elicitation, DateTime.UtcNow, args.Has("force-early"));
            if (!guard.Allowed)
                throw new InvalidOperationException(
                    $"A-5 : la dernière ligne d'élicitation date de {guard.HoursSinceElicitation:0.0} h. " +
                    "Le même jour, on reconnaît ses propres indices — on ne mesure plus que sa loyauté " +
                    "envers soi-même. Attendre 24 h, ou passer --force-early en connaissance de cause " +
                    "(l'entorse sera consignée dans le manifeste).");

            var runRefs = sourceRuns
                .Select(s => new ComparisonRunRef(s.Run.Manifest.RunId, s.Path.Replace('\\', '/')))
                .ToList();

            manifest = BuildComparisonManifest(
                guard,
                benchFile: benchPath.Replace('\\', '/'),
                benchHash: bench.Manifest.BenchHash,
                seed: seed,
                elicitationFile: elicitationPath.Replace('\\', '/'),
                runs: runRefs.AsReadOnly(),
                targetCount: args.GetInt("count", 100),
                quotaBeforePause: args.GetInt("quota", 50),
                createdAtUtc: DateTime.UtcNow);

            HumanFile.WriteComparisonManifest(outPath, manifest);
            existing = HumanFile.ReadComparisons(outPath);
        }
        else
        {
            // Reprise : le manifeste fait foi (append-only). La garde J+1 a déjà été évaluée
            // au premier démarrage et sa trace est dans la ligne 1.
            manifest = existing.Manifest;
            HumanFile.RequireBench(outPath, manifest.BenchHash, bench);
        }

        var plan = ComparisonPlan.Build(
            bench, elicitation,
            runA, runA.Manifest.RunId,
            runB, runB.Manifest.RunId,
            anchorRun, anchorRun?.Manifest.RunId,
            manifest.Seed, manifest.TargetCount);

        var session = new JudgeSession(
            bench, plan, existing, outPath,
            sessionId: $"s-{DateTime.UtcNow:yyyyMMddHHmmss}",
            quotaBeforePause: manifest.QuotaBeforePause,
            pauseSeconds: pauseSeconds);

        var pagePath = Path.Combine(AppContext.BaseDirectory, "Web", "Pages", "judge.html");
        var app = HumanServer.BuildJudgeApp(session, pagePath, port);
        await app.StartAsync(ct);

        Console.WriteLine($"séance B : {HumanServer.ResolveUrl(app)}");
        Console.WriteLine($"  fichier        : {outPath}");
        Console.WriteLine($"  lot            : {plan.Count} couple(s), seed {manifest.Seed}");
        Console.WriteLine($"  déjà jugés     : {session.CompletedCount}");
        Console.WriteLine($"  écart séance A : {manifest.HoursSinceElicitation:0.0} h" +
                          (manifest.EarlyStart ? "  ⚠ earlyStart consigné dans le manifeste" : string.Empty));
        Console.WriteLine("  Ctrl+C pour arrêter. Reprise sûre : chaque verdict est écrit à la soumission.");

        await app.WaitForShutdownAsync(ct);
        return 0;
    }

    /// <summary>
    /// Isole le calcul du manifeste de la séance B (jusqu'ici en ligne dans <see cref="Judge"/>)
    /// pour le rendre testable sans démarrer de serveur HTTP ni attendre un arrêt de processus.
    /// Le point qui compte : <see cref="JudgeGuardResult.EarlyStart"/> et
    /// <see cref="JudgeGuardResult.HoursSinceElicitation"/> sont recopiés **tels quels** dans le
    /// manifeste écrit — un contournement de la garde A-5 devient une donnée du corpus, pas un
    /// secret, et ce fait est vérifiable indépendamment de la CLI (<c>JudgeManifestTests</c>).
    /// </summary>
    internal static ComparisonManifest BuildComparisonManifest(
        JudgeGuardResult guard,
        string benchFile,
        string benchHash,
        long seed,
        string elicitationFile,
        IReadOnlyList<ComparisonRunRef> runs,
        int targetCount,
        int quotaBeforePause,
        DateTime createdAtUtc) => new(
        Kind: "manifest",
        BenchFile: benchFile,
        BenchHash: benchHash,
        Seed: seed,
        ElicitationFile: elicitationFile,
        Runs: runs,
        TargetCount: targetCount,
        QuotaBeforePause: quotaBeforePause,
        HoursSinceElicitation: guard.HoursSinceElicitation,
        EarlyStart: guard.EarlyStart,
        HarnessVersion: HumanFile.HarnessVersion,
        CreatedAtUtc: createdAtUtc);

    /// <summary>
    /// Verbe <c>human-run</c> : projette la séance A en pseudo-run au format <see cref="RunFile"/>.
    /// Les verbes existants s'appliquent alors <b>sans modification</b> — <c>decode</c> saute N3
    /// tout seul (aucun board du banc n'a ses 4 directions annotées), <c>score --subset</c> et
    /// <c>compare --subset</c> font le reste. Aucun appel LLM.
    /// </summary>
    private static int HumanRunCommand(Args args)
    {
        var elicitationPath = args.Get("elicitation")
                              ?? Path.Combine("eval", "human", "elicitation.dev.jsonl");
        var outDirectory = args.Get("out") ?? Path.Combine("eval", "runs");

        var elicitation = HumanFile.ReadElicitation(elicitationPath);
        var benchPath = args.Get("bench") ?? elicitation.Manifest.BenchFile;
        var bench = BenchFile.Read(benchPath);
        HumanFile.RequireBench(elicitationPath, elicitation.Manifest.BenchHash, bench);

        if (elicitation.Elicitations.Count == 0)
            throw new InvalidOperationException(
                $"{elicitationPath} ne contient aucune ligne d'élicitation : rien à projeter.");

        var (manifest, attempts) = HumanRunExport.Build(
            bench, elicitation, benchPath, DateTime.UtcNow);

        var path = Path.Combine(outDirectory, $"{manifest.RunId}.jsonl");
        if (File.Exists(path) && !args.Has("force"))
            throw new InvalidOperationException(
                $"{path} existe déjà. Réécrire un run décodé perdrait son .decoded.jsonl : " +
                "passer --force en connaissance de cause.");

        Directory.CreateDirectory(outDirectory);
        HumanRunExport.Write(outDirectory, manifest, attempts);

        var passCount = attempts.Count(a => a.FailureKind == HumanRunExport.PassFailureKind);

        Console.WriteLine($"pseudo-run écrit : {path}");
        Console.WriteLine($"  runId       : {manifest.RunId}");
        Console.WriteLine($"  banc        : {benchPath} (hash {manifest.BenchHash})");
        Console.WriteLine($"  directions  : {attempts.Count} dont {passCount} pass " +
                          "(A-1 : un pass reste au dénominateur, il n'est jamais retiré)");
        Console.WriteLine();
        Console.WriteLine("Suite : decode ce run, puis");
        Console.WriteLine($"  score --run {path.Replace('\\', '/')} --subset {elicitationPath.Replace('\\', '/')}");
        return 0;
    }

    /// <summary>
    /// Verbe <c>human-report</c>. Ni accord décodeur/humain ni κ : c'est P6, et les publier ici
    /// reviendrait à franchir une porte en la décrivant.
    /// </summary>
    private static int HumanReportCommand(Args args)
    {
        static string N(double v) => v.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"));

        var elicitationPath = args.Get("elicitation");
        var comparisonsPath = args.Get("comparisons");
        if (elicitationPath is null && comparisonsPath is null)
            throw new ArgumentException("Au moins --elicitation ou --comparisons est requis.");

        if (elicitationPath is not null)
        {
            var report = HumanReport.ForElicitation(HumanFile.ReadElicitation(elicitationPath));

            Console.WriteLine();
            Console.WriteLine($"séance A — {elicitationPath}");
            Console.WriteLine($"  items saisis           {report.ItemsSaved} / {report.ItemsPlanned}");
            foreach (var outcome in report.Outcomes)
                Console.WriteLine($"  {outcome.Outcome,-22} {outcome.Count}");
            Console.WriteLine($"  taux de pass           {N(report.PassRate)}   ← argument pour/contre l'intervention de rang 4");
            Console.WriteLine();
            Console.WriteLine("  chrono par issue (s)   médiane / min / max");
            foreach (var e in report.Elapsed.Where(e => e.Count > 0))
                Console.WriteLine($"    {e.Outcome,-20} {e.MedianSeconds,6:0.0} / {e.MinSeconds,3} / {e.MaxSeconds,3}   (n={e.Count})");
            Console.WriteLine();
            Console.WriteLine("  dérive de fatigue      moyenne du chrono par tranche d'items");
            foreach (var bucket in report.Fatigue)
                Console.WriteLine($"    {bucket.FromOrdinal,3}–{bucket.ToOrdinal,-3}            {bucket.MeanElapsedSeconds,6:0.0} s   (n={bucket.Count})");
            Console.WriteLine();
            Console.WriteLine("  relations");
            foreach (var relation in report.Relations)
                Console.WriteLine($"    {relation.Count,4}  {relation.RelationType}");
            Console.WriteLine();
            Console.WriteLine($"  indices assistés       {report.AssistedClueCount}");
            Console.WriteLine($"  indices refusés        {report.RejectedClueCount}");

            if (report.ItemsSaved < report.ItemsPlanned)
                Console.WriteLine($"  ⚠ SÉANCE INCOMPLÈTE : {report.ItemsSaved}/{report.ItemsPlanned}.");

            var path = Path.ChangeExtension(elicitationPath, ".report.json");
            File.WriteAllText(path, EvalJson.Serialize(report));
            Console.WriteLine($"  rapport écrit : {path}");
        }

        if (comparisonsPath is not null)
        {
            var report = HumanReport.ForComparisons(HumanFile.ReadComparisons(comparisonsPath));

            Console.WriteLine();
            Console.WriteLine($"séance B — {comparisonsPath}");
            Console.WriteLine($"  couples jugés          {report.ItemsJudged} / {report.ItemsPlanned}");
            Console.WriteLine();
            Console.WriteLine("  taux de victoire par famille");
            foreach (var family in report.Families)
                Console.WriteLine(
                    $"    {family.Family,-18} {family.Label,-22} A {N(family.OptionAWinRate)} / B {N(family.OptionBWinRate)} / = {N(family.TieRate)}   (n={family.Count})");
            Console.WriteLine();
            Console.WriteLine($"  position 1 gagne       {N(report.Position1WinRate)}");
            Console.WriteLine($"  taux d'égalité         {N(report.TieRate)}");
            Console.WriteLine($"  cohérence intra-juge   {N(report.IntraJudgeAgreement)}   (sur {report.DuplicatePairCount} doublon(s) inversé(s))");
            Console.WriteLine($"  ancres réussies        {report.AnchorCorrect} / {report.AnchorCount}");

            if (report.Position1Suspect)
                Console.WriteLine(
                    $"  ⚠ LOT SUSPECT : la position 1 gagne {N(report.Position1WinRate)}, à plus de " +
                    "10 points de 50 %. Le biais de position domine — le consigner DANS LE REGISTRE.");

            if (report.AnchorSuspect)
                Console.WriteLine(
                    "  ⚠ LOT SUSPECT : plus d'une ancre ratée. Un opérateur qui préfère un indice " +
                    "aléatoire à un indice réel a produit un lot dont on ne peut tirer aucune porte.");

            var path = Path.ChangeExtension(comparisonsPath, ".report.json");
            File.WriteAllText(path, EvalJson.Serialize(report));
            Console.WriteLine($"  rapport écrit : {path}");
        }

        Console.WriteLine();
        Console.WriteLine("Ce rapport ne calcule NI l'accord décodeur/humain NI κ — c'est P6.");
        return 0;
    }
}
