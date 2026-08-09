using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.Eval.Bench;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;

namespace SoClover.Eval.Human;

/// <summary>
/// Issue d'une opération de séance. Le mapping vers les codes HTTP vit dans
/// <c>Web/HumanServer.cs</c> : la logique de protocole ne connaît pas HTTP, ce qui la rend
/// testable sans serveur.
/// </summary>
public enum SessionStatus
{
    Ok,
    BadRequest,
    Rejected,
    Conflict,
    Finished,
}

/// <summary>
/// Ce que la page voit d'un item. <b>Ne contient jamais les candidats du modèle</b> — c'est
/// <c>/api/candidates</c>, verrouillé par A-4, qui les sert.
/// <para>
/// <see cref="TargetCells"/> donne les deux coordonnées <c>[indexCarte, indexFace]</c> de la paire
/// cible, dérivées de <c>BoardGeometry.GetEdgeMapping</c>. Surligner par géométrie plutôt que par
/// comparaison de texte évite qu'un mot répété ailleurs sur le board soit surligné à tort.
/// </para>
/// </summary>
public sealed record NextItemView(
    bool Finished,
    bool PauseRequired,
    int PauseSecondsRemaining,
    bool AwaitingReveal,
    string? BoardId,
    string? Direction,
    IReadOnlyList<IReadOnlyList<string>>? Cards,
    IReadOnlyList<string>? ReferenceWords,
    IReadOnlyList<IReadOnlyList<int>>? TargetCells,
    int ItemOrdinal,
    int CompletedCount,
    int TotalCount,
    int TimerSeconds);

public sealed record SessionResult(
    SessionStatus Status,
    string? Message,
    IReadOnlyList<string> RejectionRules);

public sealed record CandidatesView(
    SessionStatus Status,
    string? Message,
    string? Clue,
    IReadOnlyList<string> Candidates,
    string? Explanation);

/// <summary>
/// Logique du protocole A. Le serveur ne fait que traduire en HTTP : les six règles A-1 à A-6 qui
/// concernent la séance auteur sont appliquées <b>ici</b>, donc non contournables par un opérateur
/// pressé.
/// </summary>
public sealed class ElicitationSession
{
    private readonly IReadOnlyList<PlanItem> _plan;
    private readonly IReadOnlyDictionary<string, BenchBoard> _boards;
    private readonly IReadOnlyDictionary<(string BoardId, string Direction), RunAttempt> _candidates;
    private readonly IClueValidator _validator;
    private readonly Func<DateTime> _clock;
    private readonly string _path;
    private readonly string _sessionId;
    private readonly int _timerSeconds;
    private readonly int _quotaBeforePause;
    private readonly int _pauseSeconds;
    private readonly HashSet<(string BoardId, string Direction)> _done;
    private readonly List<string> _rejected = [];
    private readonly Lock _gate = new();

    private int _index;
    private int _servedForIndex = -1;
    private DateTime? _servedAtUtc;
    private DateTime? _pauseStartedAtUtc;
    private int _pauseAcknowledgedAt = -1;

    public ElicitationSession(
        BenchContents bench,
        IReadOnlyList<PlanItem> plan,
        ElicitationContents existing,
        IReadOnlyDictionary<(string BoardId, string Direction), RunAttempt> candidates,
        IClueValidator validator,
        string outputPath,
        string sessionId,
        int timerSeconds,
        int quotaBeforePause,
        int pauseSeconds,
        Func<DateTime>? clock = null)
    {
        _plan = plan;
        _boards = bench.Boards.ToDictionary(b => b.BoardId, StringComparer.Ordinal);
        _candidates = candidates;
        _validator = validator;
        _path = outputPath;
        _sessionId = sessionId;
        _timerSeconds = timerSeconds;
        _quotaBeforePause = quotaBeforePause;
        _pauseSeconds = pauseSeconds;
        _clock = clock ?? (() => DateTime.UtcNow);

        _done = existing.Elicitations.Select(e => (e.BoardId, e.Direction)).ToHashSet();
        _index = 0;
        SkipDone();
    }

    public int CompletedCount => _done.Count;

    public int TotalCount => _plan.Count;

    /// <summary>
    /// Indice retenu par direction dans le run de candidats : la dernière tentative valide, comme
    /// <c>DecodeCommand</c>. Un run absent donne un index vide — la séance reste jouable, la
    /// révélation est seulement muette.
    /// </summary>
    public static IReadOnlyDictionary<(string BoardId, string Direction), RunAttempt> BuildCandidateIndex(
        RunContents? run) =>
        run is null
            ? new Dictionary<(string, string), RunAttempt>()
            : run.Attempts
                .Where(a => a.Valid && a.Clue is not null)
                .GroupBy(a => (a.BoardId, a.Direction))
                .ToDictionary(g => g.Key, g => g.Last());

    public NextItemView Next()
    {
        lock (_gate)
        {
            if (IsFinished)
                return Empty(finished: true, pauseRequired: false, pauseSecondsRemaining: 0);

            // Item déjà consigné mais non encore révélé : on ne redémarre NI le chrono NI le
            // compteur de tentatives refusées. Un F5 ne rejoue rien.
            if (IsAwaitingReveal())
                return ItemView(awaitingReveal: true);

            if (IsPauseDue())
            {
                _pauseStartedAtUtc ??= _clock();
                var remaining = _pauseSeconds - (int)(_clock() - _pauseStartedAtUtc.Value).TotalSeconds;
                if (remaining > 0)
                    return Empty(finished: false, pauseRequired: true, pauseSecondsRemaining: remaining);

                _pauseAcknowledgedAt = _done.Count;
                _pauseStartedAtUtc = null;
            }

            if (_servedForIndex != _index)
            {
                _servedForIndex = _index;
                _servedAtUtc = _clock();
                _rejected.Clear();
            }

            return ItemView(awaitingReveal: false);
        }
    }

    public SessionResult SubmitAttempt(string? clue, string outcome, string? relationType, int elapsedSeconds)
    {
        lock (_gate)
        {
            if (IsFinished)
                return Finished();
            if (IsAwaitingReveal())
                return new SessionResult(SessionStatus.Conflict,
                    "Une tentative est déjà consignée pour cet item.", []);
            // Le chrono serveur (_servedAtUtc) et le compteur de pause (IsPauseDue) ne sont
            // établis que dans Next(). Sans cette garde, un appel direct — premier appel de la
            // séance, ou après un RecordAssist qui a fait avancer _index sans qu'on rappelle
            // Next() — soumettrait soit avec un _servedAtUtc nul (serverElapsed retomberait sur
            // la valeur fournie par le client, invérifiable), soit avec un _servedAtUtc périmé
            // (mesuré pour un autre item), et court-circuiterait silencieusement la pause de
            // quota puisque IsPauseDue() n'est consultée que par Next(). Exiger l'égalité rend
            // Next() incontournable avant toute soumission, ce qui restaure les deux garanties.
            if (_servedForIndex != _index)
                return new SessionResult(SessionStatus.Conflict,
                    "Next() doit être appelé avant toute soumission pour cet item : le chrono " +
                    "serveur n'a pas été démarré (ou est périmé).", []);
            if (!Outcomes.IsKnown(outcome))
                return Bad($"Issue inconnue : « {outcome} ». Valeurs admises : {string.Join(", ", Outcomes.All)}.");
            if (relationType is not null && !RelationTypes.IsKnown(relationType))
                return Bad($"Type de relation hors vocabulaire : « {relationType} ».");
            if (elapsedSeconds < 0)
                return Bad("elapsedSeconds doit être ≥ 0.");

            var item = _plan[_index];
            var board = _boards[item.BoardId];
            var direction = Enum.Parse<Direction>(item.Direction);

            string? accepted = null;
            if (outcome != Outcomes.Pass)
            {
                var trimmed = (clue ?? string.Empty).Trim();
                if (trimmed.Length == 0)
                    return Bad("Un indice est requis pour une issue « solide » ou « tiède ».");

                // ClueAcceptance.Check ne peut lever InvalidClueException que sur un texte
                // vide/blanc après trim ; `trimmed` est déjà non vide à ce stade (garde
                // ci-dessus), donc aucun try/catch n'est nécessaire ici.
                var check = ClueAcceptance.Check(
                    trimmed, direction, BenchBoardMapper.ToCloverBoard(board), _validator);

                // Le chrono continue : un indice refusé n'interrompt pas la mesure de difficulté.
                if (!check.IsValid)
                {
                    _rejected.Add(trimmed);
                    return new SessionResult(
                        SessionStatus.Rejected,
                        "Indice refusé par les règles du jeu.",
                        check.Errors.Select(e => e.Rule.ToString()).Distinct(StringComparer.Ordinal)
                            .ToList().AsReadOnly());
                }

                accepted = trimmed;
            }

            var now = _clock();
            var serverElapsed = _servedAtUtc is { } served
                ? (int)Math.Round((now - served).TotalSeconds)
                : elapsedSeconds;

            HumanFile.AppendElicitation(_path, new ElicitationLine(
                Kind: "elicitation",
                BoardId: item.BoardId,
                Direction: item.Direction,
                ReferenceWords: BenchBoardMapper.ReferenceWords(board, direction),
                Outcome: outcome,
                Clue: accepted,
                ElapsedSeconds: elapsedSeconds,
                ServerElapsedSeconds: serverElapsed,
                RelationType: relationType,
                RejectedAttempts: _rejected.ToList().AsReadOnly(),
                SessionId: _sessionId,
                ItemOrdinal: _done.Count + 1,
                AuthoredAtUtc: now));

            _done.Add((item.BoardId, item.Direction));
            return Ok();
        }
    }

    public CandidatesView Candidates()
    {
        lock (_gate)
        {
            if (IsFinished)
                return new CandidatesView(SessionStatus.Finished, "Séance terminée.", null, [], null);

            // A-4 est un verrou, pas un conseil : regarder avant de tenter détruirait
            // définitivement la mesure de difficulté non assistée de cet item.
            if (!IsAwaitingReveal())
                return new CandidatesView(
                    SessionStatus.Conflict,
                    "A-4 : les candidats du modèle ne sont consultables qu'après une tentative consignée.",
                    null, [], null);

            var item = _plan[_index];
            return _candidates.TryGetValue((item.BoardId, item.Direction), out var attempt)
                ? new CandidatesView(SessionStatus.Ok, null, attempt.Clue, attempt.Candidates ?? [], attempt.Explanation)
                : new CandidatesView(SessionStatus.Ok, null, null, [], null);
        }
    }

    public SessionResult RecordAssist(string? assistedClue, string? notes)
    {
        lock (_gate)
        {
            if (IsFinished)
                return Finished();
            if (!IsAwaitingReveal())
                return new SessionResult(SessionStatus.Conflict,
                    "Aucune tentative consignée pour cet item.", []);

            var item = _plan[_index];
            var clue = string.IsNullOrWhiteSpace(assistedClue) ? null : assistedClue.Trim();
            var note = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

            if (clue is not null || note is not null)
                HumanFile.AppendAssist(_path, new AssistLine(
                    Kind: "assist",
                    BoardId: item.BoardId,
                    Direction: item.Direction,
                    AssistedClue: clue,
                    Notes: note,
                    AuthoredAtUtc: _clock()));

            _index++;
            SkipDone();
            return Ok();
        }
    }

    // ── Interne ─────────────────────────────────────────────────────────────

    // Factorisée : réutilisée par Next, SubmitAttempt, Candidates et RecordAssist. Les messages
    // Conflict qui suivent cette garde restent volontairement distincts par méthode — ils portent
    // des diagnostics différents (item déjà consigné, verrou A-4, chrono non démarré) et une
    // fusion les rendrait moins précis pour un opérateur qui lit une réponse d'erreur.
    private bool IsFinished => _index >= _plan.Count;

    private void SkipDone()
    {
        while (_index < _plan.Count && _done.Contains((_plan[_index].BoardId, _plan[_index].Direction)))
            _index++;
    }

    private bool IsAwaitingReveal() =>
        _index < _plan.Count && _done.Contains((_plan[_index].BoardId, _plan[_index].Direction));

    private bool IsPauseDue() =>
        _done.Count > 0 && _done.Count % _quotaBeforePause == 0 && _pauseAcknowledgedAt != _done.Count;

    private NextItemView ItemView(bool awaitingReveal)
    {
        var item = _plan[_index];
        var board = _boards[item.BoardId];
        var direction = Enum.Parse<Direction>(item.Direction);
        var (cardA, faceA, cardB, faceB) = BoardGeometry.GetEdgeMapping(direction);

        return new NextItemView(
            Finished: false,
            PauseRequired: false,
            PauseSecondsRemaining: 0,
            AwaitingReveal: awaitingReveal,
            BoardId: item.BoardId,
            Direction: item.Direction,
            Cards: board.Cards,
            ReferenceWords: BenchBoardMapper.ReferenceWords(board, direction),
            TargetCells: [[(int)cardA, (int)faceA], [(int)cardB, (int)faceB]],
            ItemOrdinal: _done.Count + (awaitingReveal ? 0 : 1),
            CompletedCount: _done.Count,
            TotalCount: _plan.Count,
            TimerSeconds: _timerSeconds);
    }

    private NextItemView Empty(bool finished, bool pauseRequired, int pauseSecondsRemaining) =>
        new(finished, pauseRequired, pauseSecondsRemaining, false,
            null, null, null, null, null, 0, _done.Count, _plan.Count, _timerSeconds);

    private static SessionResult Ok() => new(SessionStatus.Ok, null, []);

    private static SessionResult Bad(string message) => new(SessionStatus.BadRequest, message, []);

    private static SessionResult Finished() => new(SessionStatus.Finished, "Séance terminée.", []);
}
