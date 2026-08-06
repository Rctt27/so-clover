using System.Globalization;
using SoClover.Eval.Bench;
using SoClover.Eval.Cli;
using SoClover.Eval.Io;

namespace SoClover.Eval.Analysis;

/// <summary>
/// Verbe <c>analyze</c> : taxonomie chiffrée d'un run décodé, échantillon relu à la main, et
/// matrice de confusion auto ↔ humain. <b>Aucun appel LLM.</b>
/// </summary>
public static class AnalyzeCommand
{
    public static Task<int> ExecuteAsync(Args args, CancellationToken ct)
    {
        var outDirectory = args.Get("out") ?? Path.Combine("eval", "analysis");

        if (args.Get("review") is { } reviewPath)
            return Task.FromResult(Review(reviewPath));

        var runPath = args.Require("run");
        var run = RunFile.Read(runPath);
        var bench = BenchFile.Read(run.Manifest.BenchFile);

        // La taxonomie se lit sur les DÉCODAGES, pas sur les indices.
        var decodedPath = args.Get("decoded") ?? DecodeFile.FindForRun(runPath);
        var decoded = (decodedPath is null ? null : DecodeFile.ReadOrNull(decodedPath))
            ?? throw new InvalidOperationException(
                $"Aucun décodage pour {runPath} : la taxonomie se lit sur les décodages, pas sur " +
                "les indices. Lancer `decode` d'abord.");

        var taxonomy = FailureTaxonomy.Compute(bench, run, decoded);

        Directory.CreateDirectory(outDirectory);
        var taxonomyPath = AnalysisSample.TaxonomyPathFor(outDirectory, taxonomy.RunId);
        File.WriteAllText(taxonomyPath, EvalJson.Serialize(taxonomy));

        Print(taxonomy);
        Console.WriteLine($"taxonomie écrite : {taxonomyPath}");

        if (args.Has("sample"))
        {
            var size = args.GetInt("sample", AnalysisSample.DefaultSize);
            var seed = args.GetLong("seed", 0);
            if (seed == 0) throw new ArgumentException("--seed est requis et doit être non nul.");

            var items = AnalysisSample.Draw(bench, run, decoded, taxonomy, size, seed);
            var samplePath = AnalysisSample.PathFor(outDirectory, taxonomy.RunId);

            if (File.Exists(samplePath) && !args.Has("force"))
                throw new InvalidOperationException(
                    $"{samplePath} existe déjà. Le réécrire perdrait les étiquettes humaines déjà " +
                    "posées : passer --force en connaissance de cause.");

            File.WriteAllText(samplePath, AnalysisSample.Render(taxonomy.RunId, seed, items));

            Console.WriteLine();
            Console.WriteLine($"échantillon écrit : {samplePath}");
            Console.WriteLine($"  {items.Count} item(s), seed {seed}. Remplir « étiquette humaine : » " +
                              "pour chacun, puis :");
            Console.WriteLine($"  analyze --review {samplePath.Replace('\\', '/')}");
        }

        return Task.FromResult(0);
    }

    private static int Review(string path)
    {
        static string N(double v) => v.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"));

        var items = AnalysisSample.Parse(File.ReadAllText(path));
        var matrix = AnalysisSample.Review(items);

        Console.WriteLine();
        Console.WriteLine($"relecture de {path} — {matrix.Total} item(s)");
        Console.WriteLine();
        Console.WriteLine("matrice de confusion  auto → humain");
        foreach (var row in matrix.Rows)
            Console.WriteLine($"  {row.AutoMode,-4} → {row.HumanMode,-4}   {row.Count,4}" +
                              (row.AutoMode == row.HumanMode ? "   ✓" : string.Empty));
        Console.WriteLine();
        Console.WriteLine($"accord auto ↔ humain   {N(matrix.Agreement)}   " +
                          $"(seuil indicatif {N(AnalysisSample.ReviewAgreementThreshold)})");

        if (matrix.Agreement < AnalysisSample.ReviewAgreementThreshold)
            Console.WriteLine(
                "  ⚠ sous le seuil : les seuils des signatures sont à revoir, et la révision doit " +
                "être DATÉE. Une taxonomie fausse oriente les interventions — elle coûte plus cher " +
                "qu'aucune taxonomie.");

        Console.WriteLine();
        return 0;
    }

    /// <summary>
    /// Le suffixe d'une ligne de distribution.
    /// <para>
    /// I1 : la règle des 5 % n'a de sens que sur un mode d'échec sémantique mesuré automatiquement
    /// sur les directions exploitables. <c>M5</c> n'est JAMAIS mesuré automatiquement (§5.3) ;
    /// <c>M?</c> ne dit que « aucune signature reconnue » — dans les deux cas, « ≥ 5 % →
    /// intervention justifiée » ne veut rien dire.
    /// </para>
    /// <para>
    /// <paramref name="unscorableDirectionCount"/> ne s'affiche <b>pas</b> sur la ligne <c>M?</c> :
    /// depuis I1, les directions D6 sortent du numérateur ET du dénominateur des parts, donc de
    /// cette ligne. Un « dont N sans décodage » y annonçait un sous-ensemble plus grand que
    /// l'ensemble. L'effectif est rapporté à part, en tête du rapport.
    /// </para>
    /// </summary>
    internal static string RuleSuffix(ModeCount m, int unscorableDirectionCount) => m.Mode switch
    {
        FailureModes.M0 => string.Empty,
        FailureModes.M5 => "   non extrapolé",
        FailureModes.Unclassified => unscorableDirectionCount > 0
            ? "   aucune signature reconnue — D6 exclues, comptées à part"
            : "   aucune signature reconnue",
        _ => m.ActionJustified
            ? "   ≥ 5 % → intervention justifiée"
            : "   < 5 % → aucune ligne de prompt",
    };

    private static void Print(TaxonomyReport t)
    {
        static string N(double v) => v.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"));

        Console.WriteLine();
        Console.WriteLine($"taxonomie — {t.RunId}");
        Console.WriteLine($"  directions             {t.DirectionCount}");
        Console.WriteLine($"  dont exploitables      {t.ScorableDirectionCount}");
        if (t.UnscorableDirectionCount > 0)
            Console.WriteLine(
                $"  dont SANS décodage     {t.UnscorableDirectionCount}   ← ni indice valide ni " +
                "décodage exploitable : ce n'est pas un mode d'échec sémantique");
        Console.WriteLine();
        Console.WriteLine($"  ordre de priorité      {string.Join(" → ", FailureTaxonomy.PriorityOrder)} → {FailureModes.Unclassified}");
        Console.WriteLine();
        Console.WriteLine("  mode  part     n     libellé");

        foreach (var m in t.Distribution)
            Console.WriteLine(
                $"  {m.Mode,-4}  {N(m.Share)}  {m.Count,4}  {m.Label}" +
                $"{RuleSuffix(m, t.UnscorableDirectionCount)}");

        // M6 se compte en BOARDS, jamais mélangé à la distribution par direction : ce n'est pas
        // la même unité.
        Console.WriteLine();
        Console.WriteLine($"  {FailureModes.M6}    {N(t.M6Share)}  {t.M6BoardCount,4}  " +
                          $"{FailureModes.Label(FailureModes.M6)} — sur {t.BoardCount} board(s), " +
                          "unité DISTINCTE de la distribution ci-dessus");
        if (t.M6Boards.Count > 0)
            Console.WriteLine($"        boards : {string.Join(", ", t.M6Boards)}");

        Console.WriteLine();
        Console.WriteLine(
            $"  {FailureModes.M5} n'est PAS extrapolé : le harnais n'embarque aucune ressource de " +
            "fréquence lexicale. Il se pose à la main sur l'échantillon lu (--sample).");
        Console.WriteLine();
    }
}
