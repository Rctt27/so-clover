using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using SoClover.Eval.Scoring;

namespace SoClover.Eval.Human;

/// <summary>
/// Réinjecte le rapport d'un devineur hors ligne au format d'une séance servie. Le fichier produit
/// est <b>indiscernable</b> de celui d'une séance D : mêmes champs, même manifeste, même
/// append-only — c'est ce qui permet à <c>guess-report --guessing-b</c> de fonctionner sans une
/// ligne de changement, et à <c>RequireSameMontage</c> d'attraper un kit bâti sur un autre plan.
/// <para>
/// C'est <b>ici</b>, et nulle part dans le kit, que le score apparaît : la paire de référence est
/// relue du banc committé et <c>r</c> recalculé par la formule de
/// <see cref="GuessingSession.SubmitGuess"/>. Le devineur n'a jamais rien eu à scorer, donc jamais
/// rien eu à voir.
/// </para>
/// </summary>
public static class GuessKitImport
{
    public static IReadOnlyList<GuessingLine> BuildLines(
        BenchContents bench,
        IReadOnlyList<GuessPlanItem> plan,
        GuessKitPayload rebuilt,
        KitResultContents result)
    {
        RequireSameKit(rebuilt, result.Manifest);

        var boards = bench.Boards.ToDictionary(b => b.BoardId, StringComparer.Ordinal);
        var seen = new HashSet<int>();
        var lines = new List<GuessingLine>(result.Guesses.Count);

        foreach (var guess in result.Guesses)
        {
            if (guess.ItemIndex < 0 || guess.ItemIndex >= plan.Count)
                throw new HumanIntegrityException(
                    $"itemIndex {guess.ItemIndex} hors du plan ({plan.Count} items).");

            if (!seen.Add(guess.ItemIndex))
                throw new HumanIntegrityException(
                    $"itemIndex {guess.ItemIndex} rapporté deux fois : le kit n'a pas pu produire " +
                    "ce fichier, il a été édité.");

            var item = plan[guess.ItemIndex];
            var board = boards[item.BoardId];

            if (guess.Picked.Count != 2)
                throw new HumanIntegrityException(
                    $"itemIndex {guess.ItemIndex} : {guess.Picked.Count} mot(s) désigné(s), 2 attendus.");

            if (string.Equals(guess.Picked[0], guess.Picked[1], StringComparison.Ordinal))
                throw new HumanIntegrityException(
                    $"itemIndex {guess.ItemIndex} : les deux mots désignés sont identiques.");

            // La garde décisive : les mots rapportés étaient-ils bien à l'écran de CE kit ? Un
            // rapport produit sur un autre plan (autre graine, autre run) échoue ici même si son
            // en-tête a été recopié à la main.
            var presented = GuessingSession.PresentedWords(board, bench.Manifest.BenchHash);
            foreach (var word in guess.Picked)
            {
                if (!presented.Contains(word, StringComparer.Ordinal))
                    throw new HumanIntegrityException(
                        $"itemIndex {guess.ItemIndex} : « {word} » n'était pas présenté sur ce plateau.");
            }

            var reference = BenchBoardMapper.ReferenceWords(board, Enum.Parse<Direction>(item.Direction));

            lines.Add(new GuessingLine(
                Kind: "guess",
                BoardId: item.BoardId,
                Direction: item.Direction,
                Clue: item.Clue,
                ReferenceWords: reference,
                Picked: guess.Picked,
                R: guess.Picked.Count(reference.Contains) / 2.0,
                ShuffleSeed: ShuffleSeed.ForClue(
                    bench.Manifest.BenchHash, item.BoardId, GuessingSession.PresentationDecodeIndex),
                ElapsedMs: guess.ElapsedMs,
                SessionId: result.Manifest.SessionId,
                ItemOrdinal: lines.Count + 1,
                GuessedAtUtc: guess.GuessedAtUtc));
        }

        return lines.AsReadOnly();
    }

    /// <summary>
    /// Écrit le fichier de séance. Refuse d'écraser un corpus existant : la séance D et la séance E
    /// vivent dans deux fichiers, et un <c>--out</c> qui viserait le premier détruirait H1 — c'est
    /// le piège nommé au pré-enregistrement, ici rendu impossible plutôt que déconseillé.
    /// </summary>
    public static void Write(string outPath, GuessingManifest manifest, IReadOnlyList<GuessingLine> lines)
    {
        if (File.Exists(outPath))
            throw new HumanIntegrityException(
                $"{outPath} existe déjà. L'import n'écrase jamais un corpus humain : choisir un " +
                "autre --out, ou déplacer le fichier existant si l'écrasement est voulu.");

        HumanFile.WriteGuessingManifest(outPath, manifest);
        foreach (var line in lines)
            HumanFile.AppendGuess(outPath, line);
    }

    private static void RequireSameKit(GuessKitPayload rebuilt, KitResultManifest reported)
    {
        if (reported.HarnessVersion != rebuilt.HarnessVersion)
            throw new HumanIntegrityException(
                $"Rapport en harnessVersion {reported.HarnessVersion}, harnais en " +
                $"{rebuilt.HarnessVersion}.");

        if (!string.Equals(reported.BenchHash, rebuilt.BenchHash, StringComparison.Ordinal))
            throw new MismatchedBenchException(
                $"Le rapport porte benchHash {reported.BenchHash}, le banc relu " +
                $"{rebuilt.BenchHash}.");

        if (reported.Seed != rebuilt.Seed)
            throw new MismatchedBenchException(
                $"Le rapport porte seed {reported.Seed}, le plan reconstruit {rebuilt.Seed}. " +
                "Le kit envoyé n'a pas été bâti avec ces arguments.");

        if (!string.Equals(reported.RunId, rebuilt.RunId, StringComparison.Ordinal))
            throw new MismatchedBenchException(
                $"Le rapport porte runId {reported.RunId}, le run relu {rebuilt.RunId} : les deux " +
                "devineurs n'auraient pas vu les mêmes indices.");

        // Le hachage couvre les items eux-mêmes — indices ET ordre de présentation. Deux plans qui
        // partagent banc, graine et run mais divergent par les boards exclus tombent ici.
        if (!string.Equals(reported.KitHash, rebuilt.KitHash, StringComparison.Ordinal))
            throw new MismatchedBenchException(
                $"Le rapport porte kitHash {reported.KitHash}, le plan reconstruit " +
                $"{rebuilt.KitHash}. Le kit envoyé ne correspond pas à ces arguments — vérifier " +
                "--bench, --run, --elicitation, --exclude-boards et --seed.");

        if (reported.ItemCount != rebuilt.ItemCount)
            throw new MismatchedBenchException(
                $"Le rapport annonce {reported.ItemCount} items, le plan reconstruit " +
                $"{rebuilt.ItemCount}.");
    }
}
