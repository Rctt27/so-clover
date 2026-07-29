using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Io;

namespace SoClover.Eval.Human;

/// <summary>
/// Ce que la page voit d'un couple. <b>Aucun champ de provenance</b> : ni <c>source</c>, ni
/// <c>runId</c>, ni <c>family</c>, ni <c>comparisonId</c>. L'aveuglement est ainsi structurel —
/// aucun geste de l'opérateur, aucun onglet ouvert par erreur, ne peut faire apparaître la
/// provenance pendant la séance. Elle est réattachée côté serveur au moment d'écrire la ligne.
/// </summary>
public sealed record JudgeItemView(
    bool Finished,
    bool PauseRequired,
    int PauseSecondsRemaining,
    string? BoardId,
    IReadOnlyList<IReadOnlyList<string>>? Cards,
    IReadOnlyList<string>? ReferenceWords,
    IReadOnlyList<IReadOnlyList<int>>? TargetCells,
    string? Position1Clue,
    string? Position2Clue,
    bool CanReJudge,
    int ItemOrdinal,
    int CompletedCount,
    int TotalCount);

/// <summary>Résultat de la garde A-5. <c>EarlyStart</c> devient une donnée du corpus, pas un secret.</summary>
public sealed record JudgeGuardResult(bool Allowed, double HoursSinceElicitation, bool EarlyStart);

/// <summary>
/// Logique du protocole B. Aucune échelle absolue n'existe dans cette classe : le seul verdict
/// possible est <c>A</c> / <c>B</c> / égalité. Une échelle 1-5 dériverait dans la séance et entre
/// séances, et n'aurait aucun ancrage.
/// </summary>
public sealed class JudgeSession
{
    public const double MinimumHoursBetweenSessions = 24.0;

    public const string PositionOne = "1";
    public const string PositionTwo = "2";
    public const string PositionTie = "tie";

    public const string VerdictA = "A";
    public const string VerdictB = "B";
    public const string VerdictTie = "tie";

    private readonly IReadOnlyList<ComparisonPlanItem> _plan;
    private readonly IReadOnlyDictionary<string, BenchBoard> _boards;
    private readonly Func<DateTime> _clock;
    private readonly string _path;
    private readonly string _sessionId;
    private readonly int _quotaBeforePause;
    private readonly int _pauseSeconds;
    private readonly HashSet<string> _judged;
    private readonly Lock _gate = new();

    private int _index;
    private int _lastJudgedIndex = -1;
    private DateTime? _pauseStartedAtUtc;
    private int _pauseAcknowledgedAt = -1;

    public JudgeSession(
        BenchContents bench,
        IReadOnlyList<ComparisonPlanItem> plan,
        ComparisonContents existing,
        string outputPath,
        string sessionId,
        int quotaBeforePause,
        int pauseSeconds,
        Func<DateTime>? clock = null)
    {
        _plan = plan;
        _boards = bench.Boards.ToDictionary(b => b.BoardId, StringComparer.Ordinal);
        _path = outputPath;
        _sessionId = sessionId;
        _quotaBeforePause = quotaBeforePause;
        _pauseSeconds = pauseSeconds;
        _clock = clock ?? (() => DateTime.UtcNow);

        _judged = existing.Comparisons.Select(c => c.ComparisonId).ToHashSet(StringComparer.Ordinal);
        _index = 0;
        SkipJudged();
    }

    public int CompletedCount => _judged.Count;

    public int TotalCount => _plan.Count;

    /// <summary>
    /// Garde A-5 : le verbe refuse de démarrer si la dernière ligne d'élicitation date de moins
    /// de 24 h. Le même jour, on reconnaît ses propres indices ; on ne mesure plus que sa loyauté
    /// envers soi-même. <c>forceEarly</c> autorise l'entorse et la <b>consigne</b>.
    /// </summary>
    public static JudgeGuardResult Evaluate(
        ElicitationContents elicitation, DateTime nowUtc, bool forceEarly)
    {
        if (elicitation.Elicitations.Count == 0)
            return new JudgeGuardResult(forceEarly, 0.0, forceEarly);

        var last = elicitation.Elicitations.Max(e => e.AuthoredAtUtc);
        var hours = (nowUtc - last).TotalHours;
        var naturallyAllowed = hours >= MinimumHoursBetweenSessions;

        return new JudgeGuardResult(
            Allowed: naturallyAllowed || forceEarly,
            HoursSinceElicitation: hours,
            EarlyStart: !naturallyAllowed && forceEarly);
    }

    /// <summary>
    /// Traduit un choix de <b>position</b> en verdict <b>canonique</b>. C'est le seul endroit du
    /// harnais où les deux référentiels se croisent — d'où le test dédié : même couple, les deux
    /// ordres de présentation, même gagnant canonique.
    /// </summary>
    internal static string CanonicalVerdict(string presentedOrder, string positionChoice) => positionChoice switch
    {
        PositionTie => VerdictTie,
        PositionOne => presentedOrder == PresentedOrders.Ab ? VerdictA : VerdictB,
        PositionTwo => presentedOrder == PresentedOrders.Ab ? VerdictB : VerdictA,
        _ => throw new ArgumentException($"Choix de position inconnu : « {positionChoice} ».", nameof(positionChoice)),
    };

    public JudgeItemView Next()
    {
        lock (_gate)
        {
            if (_index >= _plan.Count)
                return Empty(finished: true, pauseRequired: false, pauseSecondsRemaining: 0);

            if (IsPauseDue())
            {
                _pauseStartedAtUtc ??= _clock();
                var remaining = _pauseSeconds - (int)(_clock() - _pauseStartedAtUtc.Value).TotalSeconds;
                if (remaining > 0)
                    return Empty(finished: false, pauseRequired: true, pauseSecondsRemaining: remaining);

                _pauseAcknowledgedAt = _judged.Count;
                _pauseStartedAtUtc = null;
            }

            var item = _plan[_index];
            var board = _boards[item.BoardId];
            var (cardA, faceA, cardB, faceB) = BoardGeometry.GetEdgeMapping(Enum.Parse<Direction>(item.Direction));
            var presentedAb = item.PresentedOrder == PresentedOrders.Ab;

            return new JudgeItemView(
                Finished: false,
                PauseRequired: false,
                PauseSecondsRemaining: 0,
                BoardId: item.BoardId,
                Cards: board.Cards,
                ReferenceWords: item.ReferenceWords,
                TargetCells: [[(int)cardA, (int)faceA], [(int)cardB, (int)faceB]],
                Position1Clue: presentedAb ? item.OptionA.Clue : item.OptionB.Clue,
                Position2Clue: presentedAb ? item.OptionB.Clue : item.OptionA.Clue,
                CanReJudge: _lastJudgedIndex >= 0,
                ItemOrdinal: _judged.Count + 1,
                CompletedCount: _judged.Count,
                TotalCount: _plan.Count);
        }
    }

    public SessionResult SubmitVerdict(string positionChoice, long elapsedMs)
    {
        lock (_gate)
        {
            if (_index >= _plan.Count)
                return new SessionResult(SessionStatus.Finished, "Séance terminée.", []);

            var item = _plan[_index];
            var written = Write(item, positionChoice, elapsedMs);
            if (written.Status != SessionStatus.Ok)
                return written;

            _judged.Add(item.ComparisonId);
            _lastJudgedIndex = _index;
            _index++;
            SkipJudged();
            return written;
        }
    }

    /// <summary>
    /// Re-jugement du dernier couple : une <b>nouvelle ligne</b> est ajoutée, jamais une
    /// réécriture. Le lecteur retient la dernière ligne d'un <c>comparisonId</c>.
    /// </summary>
    public SessionResult ReJudgeLast(string positionChoice, long elapsedMs)
    {
        lock (_gate)
        {
            if (_lastJudgedIndex < 0)
                return new SessionResult(SessionStatus.Conflict, "Aucun couple à re-juger.", []);

            return Write(_plan[_lastJudgedIndex], positionChoice, elapsedMs);
        }
    }

    // ── Interne ─────────────────────────────────────────────────────────────

    private SessionResult Write(ComparisonPlanItem item, string positionChoice, long elapsedMs)
    {
        string verdict;
        try
        {
            verdict = CanonicalVerdict(item.PresentedOrder, positionChoice);
        }
        catch (ArgumentException ex)
        {
            return new SessionResult(SessionStatus.BadRequest, ex.Message, []);
        }

        if (elapsedMs < 0)
            return new SessionResult(SessionStatus.BadRequest, "elapsedMs doit être ≥ 0.", []);

        HumanFile.AppendComparison(_path, new ComparisonLine(
            Kind: "comparison",
            ComparisonId: item.ComparisonId,
            Family: item.Family,
            BoardId: item.BoardId,
            Direction: item.Direction,
            ReferenceWords: item.ReferenceWords,
            OptionA: item.OptionA,
            OptionB: item.OptionB,
            PresentedOrder: item.PresentedOrder,
            Verdict: verdict,
            ElapsedMs: elapsedMs,
            DuplicateOf: item.DuplicateOf,
            SessionId: _sessionId,
            ItemOrdinal: _judged.Count + 1,
            JudgedAtUtc: _clock()));

        return new SessionResult(SessionStatus.Ok, null, []);
    }

    private void SkipJudged()
    {
        while (_index < _plan.Count && _judged.Contains(_plan[_index].ComparisonId))
            _index++;
    }

    private bool IsPauseDue() =>
        _judged.Count > 0 && _judged.Count % _quotaBeforePause == 0 && _pauseAcknowledgedAt != _judged.Count;

    private JudgeItemView Empty(bool finished, bool pauseRequired, int pauseSecondsRemaining) =>
        new(finished, pauseRequired, pauseSecondsRemaining,
            null, null, null, null, null, null, _lastJudgedIndex >= 0,
            0, _judged.Count, _plan.Count);
}
