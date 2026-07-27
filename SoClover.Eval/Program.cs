using SoClover.Domain;
using SoClover.Eval.Cli;
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
    internal static int Main(string[] args)
    {
        var cliArgs = Args.Parse(args);

        try
        {
            return cliArgs.Verb switch
            {
                "doctor" => Doctor(),
                "" => Usage(),
                _ => Usage($"Verbe inconnu : {cliArgs.Verb}"),
            };
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
}
