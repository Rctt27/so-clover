using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Cli;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using SoClover.Eval.Scoring;
using SoClover.Infrastructure;
using SoClover.Infrastructure.AI.Prompts;

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
}
