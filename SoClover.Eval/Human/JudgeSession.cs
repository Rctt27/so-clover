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
    private int _servedForIndex = -1;
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

            // Marque l'item courant comme réellement servi. Positionné ICI seulement — jamais
            // sur un retour anticipé (fin de séance, pause due) — de sorte qu'une soumission ne
            // puisse jamais franchir une pause non purgée en sautant Next() : SubmitVerdict exige
            // plus bas _servedForIndex == _index, donc reste bloquée tant que Next() n'a pas fait
            // passer la pause ici. Même défense qu'ElicitationSession (T5) contre un client qui
            // dicterait la valeur censée l'auditer.
            _servedForIndex = _index;

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

            // Défaut Critique corrigé en revue : sans cette garde, un client pouvait consigner un
            // verdict pour un item jamais servi par Next() — et, plus grave, franchir la frontière
            // de quota sans jamais déclencher la pause (IsPauseDue n'est consultée que par Next()).
            // _servedForIndex n'est positionné que par Next(), et seulement quand il sert
            // effectivement l'item courant (jamais sur pause/fin) : exiger l'égalité rend Next()
            // incontournable avant toute soumission, ce qui restaure les deux garanties. Même
            // patron qu'ElicitationSession.SubmitAttempt (T5).
            if (_servedForIndex != _index)
                return new SessionResult(SessionStatus.Conflict,
                    "Next() doit être appelé avant toute soumission pour ce couple : il n'a pas " +
                    "encore été servi (ou une pause de quota reste à purger).", []);

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
    /// <remarks>
    /// Ne vérifie pas <c>_servedForIndex</c> : ce n'est pas une lacune symétrique à celle corrigée
    /// dans <see cref="SubmitVerdict"/>. <c>_lastJudgedIndex</c> n'est renseigné que par
    /// <see cref="SubmitVerdict"/> après une écriture réussie — donc seulement pour un couple qui
    /// a lui-même satisfait la garde <c>_servedForIndex == _index</c> au moment de son premier
    /// jugement. <c>ReJudgeLast</c> ne fait jamais progresser <c>_index</c> ni
    /// <c>_servedForIndex</c> : le couple courant (suivant) reste donc protégé par la même garde,
    /// vérifiée dans <see cref="SubmitVerdict"/> — un appel direct pour ce couple suivant, sans
    /// nouveau <c>Next()</c>, continue d'échouer après un re-jugement
    /// (voir <c>Apres_un_re_jugement_next_reste_requis_avant_le_couple_suivant</c>).
    /// </remarks>
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
