using System.Globalization;
using SoClover.Domain;
using SoClover.Eval.Analysis;
using SoClover.Eval.Bench;
using SoClover.Eval.Calibration;
using SoClover.Eval.Cli;
using SoClover.Eval.Config;
using SoClover.Eval.Decoder;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Prompts;
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
                "doctor" => DoctorAsync(CancellationToken.None),
                "bench" => Bench(cliArgs),
                "generate" => GenerateCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "decode" => DecodeCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "score" => ScoreCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "compare" => CompareCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "calibrate" => CalibrateCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "analyze" => AnalyzeCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "langfuse-sync" => LangfuseSyncCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "langfuse-pull" => LangfusePullCommand.ExecuteAsync(cliArgs, CancellationToken.None),
                "elicit" => Elicit(cliArgs, CancellationToken.None),
                "judge" => Judge(cliArgs, CancellationToken.None),
                "guess" => Guess(cliArgs, CancellationToken.None),
                "guess-kit" => Task.FromResult(GuessKitCommand(cliArgs)),
                "guess-import" => Task.FromResult(GuessImportCommand(cliArgs)),
                "guess-report" => Task.FromResult(GuessReportCommand(cliArgs)),
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
              guess     Séance D (devineur) : serveur local, 16 mots + 1 indice -> 2 mots
              guess-kit     Grave la seance decrite par --guessing dans un HTML autonome, a
                            envoyer a un devineur distant (hors ligne, sans serveur)
              guess-import  Reinjecte le rapport de ce devineur au format d'une seance servie
                            --guessing <seance de reference> --kit-result <rapport recu>
              guess-report  Δ R̄ humain vs décodeur, apparié + IC (aucun appel LLM)
                            --guessing repetable : 1 = seance D, 2 = seance E (regle ancree),
                            3+ = regle d'agregation E <= S. --guessing-b reste accepte
              human-run     Projette la séance A en pseudo-run décodable (aucun appel LLM)
              human-report  Agrégats des deux séances humaines (aucun appel LLM)
              calibrate     P6 : accord decodeur/humain, kappa, quatre portes, verdict unique
              analyze       P7 : taxonomie chiffree des modes d'echec (aucun appel LLM)
              langfuse-sync --prompts | --bench <banc.jsonl>
                            Publie dans Langfuse le contenu courant des prompts du dépôt
                            (idempotent ; refuse un conflit de version) et/ou le banc désigné comme
                            dataset, un item par direction (idempotent ; le banc de test est refusé)
              langfuse-pull --prompt <nom> --label <l> | --version <n>
                            Écrit une version Langfuse dans le fichier du dépôt (refuse sans bump)

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

            Prompts (generate, decode, calibrate) :
              --prompt-source file|langfuse     défaut : Langfuse:promptSource d'evalsettings.json ;
                                                aucun repli silencieux de l'un à l'autre
              --prompt-label <label>            défaut : production
              --prompt-version <n>              numéro de version LANGFUSE (decode : prompt clue ;
                                                le prompt board suit le label)

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
    private static async Task<int> DoctorAsync(CancellationToken ct)
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

        var langfuseOptions = EvalLlmConfig.BindLangfuse(EvalLlmConfig.BuildConfiguration());
        Console.WriteLine($"source des prompts : {langfuseOptions.PromptSource}");
        if (LangfuseClientFactory.CreateOrNull(langfuseOptions) is not { } client)
        {
            Console.WriteLine("Langfuse : aucune clé configurée — contrôle de dérive sauté.");
            return 0;
        }

        try
        {
            foreach (var prompt in SoCloverPrompt.Synchronized)
            {
                var production = await client.GetPromptAsync(prompt.LangfuseName!, "production", null, ct);
                Console.WriteLine(PromptDrift.Describe(prompt, File.ReadAllText(prompt.PackagedPath), production));
            }
        }
        catch (LangfuseException ex)
        {
            Console.WriteLine($"Langfuse : {ex.Message}");
        }
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
    /// Verdict de la séance D. Aucun appel LLM : les deux fichiers existent déjà.
    /// </summary>
    private static int GuessReportCommand(Args args)
    {
        var decodedPath = args.Require("decoded");
        var decoded = DecodeFile.ReadOrNull(decodedPath)
            ?? throw new InvalidOperationException($"Décodage illisible : {decodedPath}");

        // Le premier --guessing est H1, la séance de référence : la seule passée sur l'instrument
        // servi. `--guessing-b` reste accepté — c'est la forme écrite au registre le 2026-08-08.
        var humanPaths = args.GetAll("guessing").Concat(args.GetAll("guessing-b")).ToList();
        if (humanPaths.Count == 0)
            throw new ArgumentException("Argument requis manquant : --guessing");

        var humans = humanPaths.Select(HumanFile.ReadGuessing).ToList();

        // K = 2 : la règle ancrée sur H1, pré-enregistrée le 2026-08-08, GOUVERNE — la règle
        // générale est rapportée en regard. K ≥ 3 : la règle d'agrégation du 2026-08-09 gouverne
        // seule. Aucune généralisation symétrique ne se réduit exactement à une règle ancrée sur
        // un humain particulier ; laquelle gouverne est déclarée d'avance, pas choisie après coup.
        if (humans.Count == 2)
        {
            GuessDispersionReport(humans[0], humans[1], decoded);
            Console.WriteLine();
            return GuessCohortReport(humans, decoded, governing: false);
        }

        if (humans.Count > 2)
            return GuessCohortReport(humans, decoded, governing: true);

        var result = GuessingComparison.Compare(humans[0], decoded);

        Console.WriteLine("séance D — humain devineur vs décodeur");
        Console.WriteLine($"  directions appariées : {result.PairedDirectionCount}");
        Console.WriteLine($"  R̄ humain             : {result.HumanRecovery:0.000}");
        Console.WriteLine($"  R̄ décodeur           : {result.DecoderRecovery:0.000}");
        Console.WriteLine(
            $"  Δ (humain − décodeur) : {result.Delta:+0.000;-0.000;0.000}   " +
            $"IC 95 % [{result.CiLow:+0.000;-0.000;0.000} ; {result.CiHigh:+0.000;-0.000;0.000}]");
        Console.WriteLine();
        Console.WriteLine($"VERDICT : {result.Verdict}");
        foreach (var reason in result.Reasons)
            Console.WriteLine($"    — {reason}");

        return 0;
    }

    /// <summary>
    /// Séance E : |Δ(H1,D)| ≤ |Δ(H1,H2)| ? Le critère et ses trois issues sont pré-enregistrés au
    /// registre le 2026-08-08 ; ce rapport les affiche, il ne les choisit pas.
    /// </summary>
    private static int GuessDispersionReport(
        GuessingContents h1, GuessingContents h2, DecodeContents decoded)
    {
        var result = GuessingDispersion.Compare(h1, h2, decoded);

        static string Ci(double low, double high) =>
            $"IC 95 % [{low:+0.000;-0.000;0.000} ; {high:+0.000;-0.000;0.000}]";

        Console.WriteLine("séance E — dispersion entre deux devineurs humains");
        Console.WriteLine($"  directions appariées : {result.PairedDirectionCount}");
        Console.WriteLine($"  R̄ H1                 : {result.H1Recovery:0.000}");
        Console.WriteLine($"  R̄ H2                 : {result.H2Recovery:0.000}");
        Console.WriteLine($"  R̄ décodeur           : {result.DecoderRecovery:0.000}");
        Console.WriteLine();
        Console.WriteLine(
            $"  Δ (H1 − H2)  : {result.DeltaH1H2:+0.000;-0.000;0.000}   " +
            Ci(result.CiLowH1H2, result.CiHighH1H2) + "   ← l'échelle");
        Console.WriteLine(
            $"  Δ (H1 − D)   : {result.DeltaH1Decoder:+0.000;-0.000;0.000}   " +
            Ci(result.CiLowH1Decoder, result.CiHighH1Decoder) + "   ← rappel séance D");
        Console.WriteLine(
            $"  Δ (H2 − D)   : {result.DeltaH2Decoder:+0.000;-0.000;0.000}   " +
            Ci(result.CiLowH2Decoder, result.CiHighH2Decoder) + "   ← contrôle");
        Console.WriteLine();
        Console.WriteLine(
            $"  critère |Δ(H1,D)| ≤ |Δ(H1,H2)| : {(result.CriterionMet ? "vérifié" : "en défaut")}");
        Console.WriteLine();
        Console.WriteLine($"VERDICT : {result.Verdict}");
        foreach (var reason in result.Reasons)
            Console.WriteLine($"    — {reason}");

        return 0;
    }

    /// <summary>
    /// Règle d'agrégation pour K devineurs, pré-enregistrée au registre le 2026-08-09 avant le
    /// premier import. <paramref name="governing"/> distingue le verdict qui tranche de celui qui
    /// n'est rapporté que pour la continuité (K = 2) — sans quoi deux verdicts s'afficheraient
    /// sans qu'on sache lequel fait foi.
    /// </summary>
    private static int GuessCohortReport(
        IReadOnlyList<GuessingContents> humans, DecodeContents decoded, bool governing)
    {
        var result = GuessingCohort.Compare(humans, decoded);

        static string Ci(double low, double high) =>
            $"[{low:+0.000;-0.000;0.000} ; {high:+0.000;-0.000;0.000}]";

        Console.WriteLine(
            $"cohorte — {result.HumanCount} devineurs humains" +
            (governing ? string.Empty : "   (continuité ; à K = 2 la règle ancrée gouverne)"));
        Console.WriteLine($"  directions appariées : {result.PairedDirectionCount}");

        foreach (var human in result.Humans)
            Console.WriteLine(
                $"    R̄ {human.SessionId,-24} : {human.Recovery:0.000}" +
                (human.ViaKit ? "   (kit)" : "   (servie)"));

        Console.WriteLine($"    R̄ décodeur                 : {result.DecoderRecovery:0.000}");
        Console.WriteLine();
        Console.WriteLine(
            $"  paires humaines — IC au niveau corrigé {result.CorrectedAlpha:0.####} (Bonferroni)");

        foreach (var pair in result.HumanPairs)
            Console.WriteLine(
                $"    {pair.LeftSessionId} − {pair.RightSessionId} : " +
                $"{pair.Delta:+0.000;-0.000;0.000} {Ci(pair.CiLow, pair.CiHigh)}" +
                (pair.Separated ? "   ← séparée" : string.Empty));

        Console.WriteLine();
        Console.WriteLine("  écarts au décodeur — IC 95 %");
        foreach (var gap in result.DecoderGaps)
            Console.WriteLine(
                $"    {gap.LeftSessionId} − décodeur : " +
                $"{gap.Delta:+0.000;-0.000;0.000} {Ci(gap.CiLow, gap.CiHigh)}");

        Console.WriteLine();
        Console.WriteLine(
            $"  S (échelle, moyenne des {result.HumanPairs.Count} paires humaines) : " +
            $"{result.HumanDispersion:0.000}");
        Console.WriteLine(
            $"  E (quantité, moyenne des {result.DecoderGaps.Count} écarts au décodeur) : " +
            $"{result.DecoderDistance:0.000}");

        if (result.KitOnlyDispersion is { } kitOnly)
            Console.WriteLine(
                $"  S_kit ({result.KitOnlyPairCount} paire(s) kit ↔ kit, diagnostic) : {kitOnly:0.000}");

        Console.WriteLine();
        Console.WriteLine($"  critère E ≤ S : {(result.CriterionMet ? "vérifié" : "en défaut")}");
        Console.WriteLine();
        Console.WriteLine($"{(governing ? "VERDICT" : "verdict (non gouvernant)")} : {result.Verdict}");
        foreach (var reason in result.Reasons)
            Console.WriteLine($"    — {reason}");

        return 0;
    }

    /// <summary>
    /// Séance D « devineur » (P6). L'auteur devine à partir d'indices déjà générés : aucun appel
    /// LLM, ni générateur ni décodeur.
    /// <para>
    /// Les boards éligibles sont ceux que l'auteur <b>n'a jamais vus</b>. Deux sources d'exclusion,
    /// et il faut les deux : <c>--elicitation</c> retire les boards parcourus en séance A, que le
    /// fichier consigne ; <c>--exclude-boards</c> retire ceux exposés <b>hors protocole</b>, que
    /// rien ne consigne — le 2026-08-07, trois boards l'ont été pendant un diagnostic de
    /// désaccords. La liste retenue part au manifeste : sans elle, un lecteur ne pourrait pas
    /// savoir pourquoi le lot ne couvre pas le banc.
    /// </para>
    /// </summary>
    private static async Task<int> Guess(Args args, CancellationToken ct)
    {
        var benchPath = args.Require("bench");
        var runPath = args.Require("run");
        var outPath = args.Get("out") ?? Path.Combine("eval", "human", "guessing.dev.jsonl");
        var port = args.GetInt("port", 5179);

        var bench = BenchFile.Read(benchPath);
        var run = RunFile.Read(runPath);

        if (run.Manifest.BenchHash != bench.Manifest.BenchHash)
            throw new InvalidOperationException(
                $"{runPath} porte benchHash {run.Manifest.BenchHash}, incompatible avec {bench.Manifest.BenchHash}.");

        var existing = HumanFile.ReadGuessingOrNull(outPath);
        GuessingManifest manifest;

        if (existing is null)
        {
            manifest = NewGuessingManifest(args, bench, run, benchPath, runPath);
            HumanFile.WriteGuessingManifest(outPath, manifest);
            existing = HumanFile.ReadGuessing(outPath);
        }
        else
        {
            // Reprise : le manifeste fait foi, exclusions comprises. Les recalculer depuis la CLI
            // laisserait un lot changer de composition en cours de séance, sans rien signaler.
            manifest = existing.Manifest;
            HumanFile.RequireBench(outPath, manifest.BenchHash, bench);
        }

        var plan = GuessingPlan.Build(
            bench, run, manifest.ExcludedBoardIds.ToHashSet(StringComparer.Ordinal), manifest.Seed);

        var session = new GuessingSession(
            bench, plan, existing, outPath, sessionId: $"s-{DateTime.UtcNow:yyyyMMddHHmmss}");

        var pagePath = Path.Combine(AppContext.BaseDirectory, "Web", "Pages", "guess.html");
        var app = HumanServer.BuildGuessApp(session, pagePath, port);
        await app.StartAsync(ct);

        Console.WriteLine($"séance D : {HumanServer.ResolveUrl(app)}");
        Console.WriteLine($"  fichier        : {outPath}");
        Console.WriteLine($"  indices        : run {manifest.RunId}");
        Console.WriteLine($"  lot            : {plan.Count} direction(s), seed {manifest.Seed}");
        Console.WriteLine($"  boards exclus  : {manifest.ExcludedBoardIds.Count}");
        Console.WriteLine($"  déjà devinées  : {session.CompletedCount}");
        Console.WriteLine("  Ctrl+C pour arrêter. Reprise sûre : chaque réponse est écrite à la soumission.");

        await app.WaitForShutdownAsync(ct);
        return 0;
    }

    /// <summary>
    /// Manifeste d'une séance D neuve : c'est <b>le seul moment</b> où les exclusions se calculent
    /// depuis la ligne de commande. Ensuite — reprise, kit hors ligne, import — le manifeste écrit
    /// fait foi, sans quoi le lot pourrait changer de composition sans que rien ne le signale.
    /// </summary>
    internal static GuessingManifest NewGuessingManifest(
        Args args, BenchContents bench, RunContents run, string benchPath, string runPath)
    {
        var elicitationPath = args.Get("elicitation");
        var excluded = new SortedSet<string>(StringComparer.Ordinal);

        if (elicitationPath is not null)
        {
            var elicitation = HumanFile.ReadElicitation(elicitationPath);
            HumanFile.RequireBench(elicitationPath, elicitation.Manifest.BenchHash, bench);
            foreach (var line in elicitation.Elicitations)
                excluded.Add(line.BoardId);
        }

        foreach (var boardId in (args.Get("exclude-boards") ?? string.Empty)
                 .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            excluded.Add(boardId);

        return new GuessingManifest(
            Kind: "manifest",
            BenchFile: benchPath.Replace('\\', '/'),
            BenchHash: bench.Manifest.BenchHash,
            Seed: args.GetLong("seed", 0),
            RunId: run.Manifest.RunId,
            RunFile: runPath.Replace('\\', '/'),
            ElicitationFile: elicitationPath?.Replace('\\', '/'),
            ExcludedBoardIds: excluded.ToList().AsReadOnly(),
            TargetCount: 0,
            HarnessVersion: RunFile.HarnessVersion,
            CreatedAtUtc: DateTime.UtcNow);
    }

    /// <summary>
    /// Reconstruit le plan et la charge utile du kit <b>depuis le manifeste de la séance de
    /// référence</b>, jamais depuis la ligne de commande. Appelé à l'identique par
    /// <see cref="GuessKitCommand"/> (pour graver) et par <see cref="GuessImportCommand"/> (pour
    /// vérifier) : c'est cette symétrie qui donne son sens au <c>kitHash</c>.
    /// <para>
    /// La leçon est datée. Le 2026-08-09, recalculer les exclusions depuis <c>--elicitation</c> a
    /// rendu 53 directions là où H1 en avait devinées 42 : trois boards avaient été exclus à la
    /// main pendant un diagnostic, et aucun argument de la CLI ne s'en souvient. Le manifeste, lui,
    /// s'en souvient — c'est déjà la règle de la reprise dans <see cref="Guess"/>, elle vaut
    /// a fortiori quand la séance part chez quelqu'un d'autre.
    /// </para>
    /// </summary>
    private static (BenchContents Bench, GuessingManifest Manifest,
                    IReadOnlyList<GuessPlanItem> Plan, GuessKitPayload Payload) ResolveKit(Args args)
    {
        var guessingPath = args.Require("guessing");
        var manifest = HumanFile.ReadGuessing(guessingPath).Manifest;

        // Les chemins du manifeste font défaut ; --bench / --run ne servent qu'à rattraper un
        // fichier déplacé, et les gardes ci-dessous refusent alors tout ce qui n'est pas le même.
        var bench = BenchFile.Read(args.Get("bench") ?? manifest.BenchFile);
        var run = RunFile.Read(args.Get("run") ?? manifest.RunFile);

        HumanFile.RequireBench(guessingPath, manifest.BenchHash, bench);

        if (run.Manifest.RunId != manifest.RunId)
            throw new MismatchedBenchException(
                $"{guessingPath} porte runId {manifest.RunId}, le run relu {run.Manifest.RunId} : " +
                "les deux devineurs ne verraient pas les mêmes indices.");

        var plan = GuessingPlan.Build(
            bench, run, manifest.ExcludedBoardIds.ToHashSet(StringComparer.Ordinal), manifest.Seed);

        return (bench, manifest, plan,
            GuessKit.BuildPayload(bench, plan, manifest, DateTime.UtcNow));
    }

    /// <summary>
    /// Grave la séance dans un HTML autonome. Le fichier produit s'ouvre sans serveur et sans
    /// réseau : c'est le seul moyen de faire deviner quelqu'un d'autre que soi sans mettre
    /// l'instrument en ligne.
    /// </summary>
    private static int GuessKitCommand(Args args)
    {
        var (_, manifest, plan, payload) = ResolveKit(args);
        var outPath = args.Get("out") ?? Path.Combine("eval", "human", "seance-e.html");

        var pagePath = Path.Combine(AppContext.BaseDirectory, "Web", "Pages", "guess.html");
        var kit = GuessKitPage.Build(File.ReadAllText(pagePath), payload);

        var directory = Path.GetDirectoryName(Path.GetFullPath(outPath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(outPath, kit);

        Console.WriteLine($"kit de séance : {outPath}");
        Console.WriteLine($"  indices        : run {manifest.RunId}");
        Console.WriteLine($"  lot            : {plan.Count} direction(s), seed {manifest.Seed}");
        Console.WriteLine($"  boards exclus  : {manifest.ExcludedBoardIds.Count}");
        Console.WriteLine($"  kitHash        : {payload.KitHash}");
        Console.WriteLine($"  poids          : {new FileInfo(outPath).Length / 1024} Kio");
        Console.WriteLine();
        Console.WriteLine("  À transmettre tel quel. Le devineur ouvre le fichier dans son");
        Console.WriteLine("  navigateur, répond, puis clique « Enregistrer mes réponses » et");
        Console.WriteLine($"  renvoie seance-e-{payload.KitHash}.jsonl.");
        Console.WriteLine("  Aucune paire de référence n'est présente dans ce fichier ; le plan");
        Console.WriteLine("  complet, lui, y est — concession déclarée au registre.");
        return 0;
    }

    /// <summary>
    /// Réinjecte le rapport rapporté. Le fichier écrit est celui d'une séance servie :
    /// <c>guess-report --guessing-b</c> le consomme sans savoir qu'il vient d'un kit.
    /// </summary>
    private static int GuessImportCommand(Args args)
    {
        var (bench, manifest, plan, payload) = ResolveKit(args);
        var result = KitResultFile.Read(args.Require("kit-result"));

        // Défaut dérivé de la séance, jamais fixe : quand plusieurs personnes jouent le même kit
        // chacune de son côté, un défaut constant ferait échouer le deuxième import — ou pire,
        // inviterait à écraser le premier.
        var outPath = args.Get("out") ?? Path.Combine(
            "eval", "human",
            $"guessing.{result.Manifest.Label ?? result.Manifest.SessionId}.jsonl");
        var lines = GuessKitImport.BuildLines(bench, plan, payload, result);

        // Le manifeste de la séance de référence, à la date de création près : les deux corpus
        // doivent déclarer le même montage, c'est ce que RequireSameMontage vérifiera au report.
        GuessKitImport.Write(outPath, manifest with { CreatedAtUtc = DateTime.UtcNow }, lines);

        var answered = lines.Count;
        Console.WriteLine($"import du kit : {outPath}");
        Console.WriteLine($"  devineur       : {result.Manifest.Label ?? "— (non étiqueté)"}");
        Console.WriteLine($"  session        : {result.Manifest.SessionId}");
        Console.WriteLine($"  navigateur     : {result.Manifest.UserAgent ?? "—"}");
        Console.WriteLine($"  kitHash        : {payload.KitHash} (concordant)");
        Console.WriteLine($"  réponses       : {answered} / {plan.Count}");

        if (answered > 0)
            Console.WriteLine($"  R̄             : {lines.Average(l => l.R):0.000}");

        if (answered < plan.Count)
            Console.WriteLine(
                $"  AVERTISSEMENT : séance incomplète ({plan.Count - answered} direction(s) sans " +
                "réponse). L'appariement de guess-report les écartera des trois séries.");

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
            // Même libellé que dans `calibrate`, où deux compteurs d'ancres cohabitent : les deux
            // sorties doivent se lire l'une contre l'autre sans traduction.
            Console.WriteLine(
                $"  {CalibrateCommand.JudgeAnchorLabel,-20} {report.AnchorCorrect} / {report.AnchorCount}   réussies");

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
