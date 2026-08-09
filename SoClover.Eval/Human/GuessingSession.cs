using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;

namespace SoClover.Eval.Human;

/// <summary>
/// Ce que la page voit d'une direction à deviner. <b>Ni paire de référence, ni identifiant de
/// board</b> — même aveuglement structurel que <see cref="JudgeItemView"/>, et pour une raison de
/// plus : afficher le <c>boardId</c> laisserait reconnaître un plateau déjà vu et exploiter la
/// mémoire que la dispersion du plan cherche justement à refroidir (cinquième limite du
/// pré-enregistrement). La provenance est réattachée côté serveur au moment d'écrire la ligne.
/// </summary>
public sealed record GuessItemView(
    bool Finished,
    IReadOnlyList<string>? PresentedWords,
    string? Clue,
    int ItemOrdinal,
    int CompletedCount,
    int TotalCount);

/// <summary>
/// Logique du protocole D. La tâche est <b>exactement</b> celle du décodeur mono-indice : seize
/// mots présentés dans l'ordre de <see cref="ShuffleSeed.ForClue"/>, un indice, deux mots à
/// désigner. Toute divergence de présentation rendrait la comparaison humain / décodeur
/// approximative, et c'est précisément ce que la séance cherche à mesurer.
/// </summary>
public sealed class GuessingSession
{
    /// <summary>
    /// Indice de décodage dont on reprend l'ordre de présentation. Fixé à 0 par le
    /// pré-enregistrement : l'humain devine une fois, le décodeur trois — on lui donne l'ordre du
    /// premier de ses tirages, pas un ordre qui lui serait propre.
    /// </summary>
    public const int PresentationDecodeIndex = 0;

    private readonly IReadOnlyList<GuessPlanItem> _plan;
    private readonly IReadOnlyDictionary<string, BenchBoard> _boards;
    private readonly string _benchHash;
    private readonly Func<DateTime> _clock;
    private readonly string _path;
    private readonly string _sessionId;
    private readonly HashSet<(string BoardId, string Direction)> _guessed;
    private readonly Lock _gate = new();

    private int _index;
    private int _servedForIndex = -1;

    public GuessingSession(
        BenchContents bench,
        IReadOnlyList<GuessPlanItem> plan,
        GuessingContents existing,
        string outputPath,
        string sessionId,
        Func<DateTime>? clock = null)
    {
        _plan = plan;
        _boards = bench.Boards.ToDictionary(b => b.BoardId, StringComparer.Ordinal);
        _benchHash = bench.Manifest.BenchHash;
        _path = outputPath;
        _sessionId = sessionId;
        _clock = clock ?? (() => DateTime.UtcNow);

        _guessed = existing.Guesses.Select(g => (g.BoardId, g.Direction)).ToHashSet();
        _index = 0;
        SkipGuessed();
    }

    public int CompletedCount => _guessed.Count;

    public int TotalCount => _plan.Count;

    public GuessItemView Next()
    {
        lock (_gate)
        {
            if (_index >= _plan.Count)
                return new GuessItemView(true, null, null, 0, _guessed.Count, _plan.Count);

            _servedForIndex = _index;
            var item = _plan[_index];

            return new GuessItemView(
                Finished: false,
                PresentedWords: Presented(item),
                Clue: item.Clue,
                ItemOrdinal: _guessed.Count + 1,
                CompletedCount: _guessed.Count,
                TotalCount: _plan.Count);
        }
    }

    public SessionResult SubmitGuess(IReadOnlyList<string> picked, long elapsedMs)
    {
        lock (_gate)
        {
            if (_index >= _plan.Count)
                return new SessionResult(SessionStatus.Finished, "Séance terminée.", []);

            // Next() reste incontournable : sans cette garde, un client pourrait consigner une
            // réponse pour une direction jamais servie. Même patron que JudgeSession (T5).
            if (_servedForIndex != _index)
                return new SessionResult(SessionStatus.Conflict,
                    "Next() doit être appelé avant toute soumission pour cette direction.", []);

            if (elapsedMs < 0)
                return new SessionResult(SessionStatus.BadRequest, "elapsedMs doit être ≥ 0.", []);

            var item = _plan[_index];
            var presented = Presented(item);

            if (picked.Count != 2)
                return new SessionResult(SessionStatus.BadRequest,
                    $"Il faut exactement deux mots, {picked.Count} reçu(s).", []);

            if (string.Equals(picked[0], picked[1], StringComparison.Ordinal))
                return new SessionResult(SessionStatus.BadRequest,
                    "Les deux mots doivent être différents.", []);

            foreach (var word in picked)
            {
                if (!presented.Contains(word, StringComparer.Ordinal))
                    return new SessionResult(SessionStatus.BadRequest,
                        $"« {word} » n'est pas un mot de ce plateau.", []);
            }

            var board = _boards[item.BoardId];
            var reference = BenchBoardMapper.ReferenceWords(board, Enum.Parse<Direction>(item.Direction));

            HumanFile.AppendGuess(_path, new GuessingLine(
                Kind: "guess",
                BoardId: item.BoardId,
                Direction: item.Direction,
                Clue: item.Clue,
                ReferenceWords: reference,
                Picked: picked,
                R: picked.Count(reference.Contains) / 2.0,
                ShuffleSeed: ShuffleSeed.ForClue(_benchHash, item.BoardId, PresentationDecodeIndex),
                ElapsedMs: elapsedMs,
                SessionId: _sessionId,
                ItemOrdinal: _guessed.Count + 1,
                GuessedAtUtc: _clock()));

            _guessed.Add((item.BoardId, item.Direction));
            _index++;
            SkipGuessed();
            return new SessionResult(SessionStatus.Ok, null, []);
        }
    }

    /// <summary>
    /// Les seize mots tels que le devineur les voit. <b>Point d'entrée unique</b> : le kit hors
    /// ligne de la séance E le rappelle pour graver sa présentation, et l'import s'en sert pour
    /// vérifier que les mots désignés étaient bien à l'écran. Recopier la formule ailleurs
    /// laisserait H2 deviner sur un autre ordre que H1 sans que rien ne le signale.
    /// </summary>
    public static IReadOnlyList<string> PresentedWords(BenchBoard board, string benchHash) =>
        ShuffleSeed.Shuffle(
            BenchBoardMapper.AllWords(board),
            ShuffleSeed.ForClue(benchHash, board.BoardId, PresentationDecodeIndex));

    private IReadOnlyList<string> Presented(GuessPlanItem item) =>
        PresentedWords(_boards[item.BoardId], _benchHash);

    private void SkipGuessed()
    {
        while (_index < _plan.Count && _guessed.Contains((_plan[_index].BoardId, _plan[_index].Direction)))
            _index++;
    }
}
