using System.Reflection;
using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class JudgeSessionTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"judge-{Guid.NewGuid():N}.jsonl");
    private readonly BenchContents _bench = HumanTestData.Bench(boardCount: 40);
    private DateTime _now = new(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc);

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private (JudgeSession Session, IReadOnlyList<ComparisonPlanItem> Plan) NewSession(
        int targetCount = 30, int quota = 50, int pauseSeconds = 0)
    {
        var annotated = ElicitationPlan.Build(_bench, seed: 42, targetCount: 40);
        var elicitation = HumanTestData.Elicitation(_bench, annotated, assistCount: 12);
        var plan = ComparisonPlan.Build(
            _bench, elicitation,
            HumanTestData.Run(_bench, "run-a", "modelA"), "run-a",
            HumanTestData.Run(_bench, "run-b", "modelB"), "run-b",
            HumanTestData.Run(_bench, "run-rnd", "aleatoire"), "run-rnd",
            seed: 20260730001, targetCount);

        if (!File.Exists(_path))
            HumanFile.WriteComparisonManifest(_path, HumanTestData.ComparisonManifest(
                _bench.Manifest.BenchHash, plan.Count, quota));

        var session = new JudgeSession(
            _bench, plan, HumanFile.ReadComparisons(_path), _path, "s-judge",
            quotaBeforePause: quota, pauseSeconds: pauseSeconds, clock: () => _now);

        return (session, plan);
    }

    [Fact]
    public void La_garde_J_plus_1_refuse_une_seance_le_meme_jour()
    {
        var annotated = ElicitationPlan.Build(_bench, seed: 42, targetCount: 4);
        var elicitation = HumanTestData.Elicitation(
            _bench, annotated, authoredAtUtc: new DateTime(2026, 7, 31, 9, 0, 0, DateTimeKind.Utc));

        var tooEarly = JudgeSession.Evaluate(elicitation, _now, forceEarly: false);

        Assert.False(tooEarly.Allowed);
        Assert.InRange(tooEarly.HoursSinceElicitation, 0, 24);
        Assert.False(tooEarly.EarlyStart);
    }

    [Fact]
    public void Le_contournement_de_la_garde_est_trace()
    {
        var annotated = ElicitationPlan.Build(_bench, seed: 42, targetCount: 4);
        var elicitation = HumanTestData.Elicitation(
            _bench, annotated, authoredAtUtc: new DateTime(2026, 7, 31, 9, 0, 0, DateTimeKind.Utc));

        var forced = JudgeSession.Evaluate(elicitation, _now, forceEarly: true);

        Assert.True(forced.Allowed);
        Assert.True(forced.EarlyStart);
    }

    [Fact]
    public void Apres_vingt_quatre_heures_la_seance_demarre_sans_marque()
    {
        var annotated = ElicitationPlan.Build(_bench, seed: 42, targetCount: 4);
        var elicitation = HumanTestData.Elicitation(
            _bench, annotated, authoredAtUtc: new DateTime(2026, 7, 29, 9, 0, 0, DateTimeKind.Utc));

        var allowed = JudgeSession.Evaluate(elicitation, _now, forceEarly: false);

        Assert.True(allowed.Allowed);
        Assert.False(allowed.EarlyStart);
        Assert.True(allowed.HoursSinceElicitation >= JudgeSession.MinimumHoursBetweenSessions);
    }

    [Theory]
    [InlineData("AB", "1", "A")]
    [InlineData("AB", "2", "B")]
    [InlineData("BA", "1", "B")]
    [InlineData("BA", "2", "A")]
    [InlineData("AB", "tie", "tie")]
    [InlineData("BA", "tie", "tie")]
    public void Le_verdict_canonique_ne_depend_jamais_de_la_position(
        string presentedOrder, string positionChoice, string expected)
    {
        Assert.Equal(expected, JudgeSession.CanonicalVerdict(presentedOrder, positionChoice));
    }

    // Nom corrigé en revue : ce test n'exerce, par exécution, que l'ordre que le seed assigne
    // réellement à plan[0] (un seul des deux ordres, pas « les deux » comme l'ancien nom le
    // prétendait). La propriété générale — les deux ordres, les deux positions — est prouvée par
    // le [Theory] voisin (Le_verdict_canonique_ne_depend_jamais_de_la_position). Ce test-ci est un
    // test d'intégration : il vérifie que le plan RÉEL produit par ComparisonPlan.Build (et pas
    // seulement CanonicalVerdict en isolation) respecte l'invariant, quel que soit l'ordre tiré
    // pour cet item précis — d'où l'assertion explicite sur l'ordre effectivement rencontré.
    [Fact]
    public void Le_gagnant_canonique_correspond_a_lindice_affiche_en_position_1_quel_que_soit_lordre_tire()
    {
        // L'invariant le plus dangereux du cycle : une inversion silencieuse rendrait faux à la
        // fois le taux de victoire du modèle et le contrôle de biais de position, sans qu'aucun
        // test fonctionnel ne s'en aperçoive.
        var (session, plan) = NewSession(targetCount: 30);

        var first = session.Next();
        Assert.NotNull(first.Position1Clue);

        var current = plan[0];
        // Documente l'ordre effectivement exercé par cette exécution (déterministe par seed) :
        // ni AB ni BA n'est privilégié par construction du test.
        Assert.True(current.PresentedOrder is PresentedOrders.Ab or PresentedOrders.Ba);

        var winnerClue = current.PresentedOrder == PresentedOrders.Ab
            ? current.OptionA.Clue
            : current.OptionB.Clue;

        // On choisit toujours la position 1 : le gagnant canonique doit être l'option
        // effectivement affichée en position 1, quel que soit l'ordre tiré.
        Assert.Equal(first.Position1Clue, winnerClue);

        session.SubmitVerdict(JudgeSession.PositionOne, elapsedMs: 9000);

        var line = Assert.Single(HumanFile.ReadComparisons(_path).Comparisons);
        var canonicalWinner = line.Verdict == JudgeSession.VerdictA ? line.OptionA : line.OptionB;
        Assert.Equal(winnerClue, canonicalWinner.Clue);
    }

    [Fact]
    public void Le_re_jugement_ajoute_une_ligne_et_ne_reecrit_jamais()
    {
        var (session, _) = NewSession();
        session.Next();
        session.SubmitVerdict(JudgeSession.PositionOne, 9000);

        Assert.Equal(SessionStatus.Ok, session.ReJudgeLast(JudgeSession.PositionTwo, 4000).Status);

        var contents = HumanFile.ReadComparisons(_path);
        Assert.Equal(2, contents.Comparisons.Count);
        Assert.Equal(contents.Comparisons[0].ComparisonId, contents.Comparisons[1].ComparisonId);

        var latest = HumanFile.LatestByComparisonId(contents)[contents.Comparisons[0].ComparisonId];
        Assert.Equal(contents.Comparisons[1].Verdict, latest.Verdict);
    }

    [Fact]
    public void La_reprise_repart_au_premier_couple_non_juge()
    {
        var (first, _) = NewSession();
        first.Next();
        first.SubmitVerdict(JudgeSession.PositionOne, 9000);
        first.Next();
        first.SubmitVerdict(JudgeSession.PositionTie, 12000);

        var (resumed, _) = NewSession();

        Assert.Equal(2, resumed.CompletedCount);
        Assert.Equal(3, resumed.Next().ItemOrdinal);
    }

    [Fact]
    public void La_pause_arrive_au_quota_de_la_seance_B()
    {
        var (session, _) = NewSession(targetCount: 30, quota: 2, pauseSeconds: 60);

        for (var i = 0; i < 2; i++)
        {
            session.Next();
            session.SubmitVerdict(JudgeSession.PositionOne, 8000);
        }

        Assert.True(session.Next().PauseRequired);

        _now = _now.AddSeconds(61);
        Assert.False(session.Next().PauseRequired);
    }

    [Fact]
    public void Un_choix_de_position_inconnu_est_rejete()
    {
        var (session, _) = NewSession();
        session.Next();

        Assert.Equal(SessionStatus.BadRequest, session.SubmitVerdict("3", 1000).Status);
        Assert.Empty(HumanFile.ReadComparisons(_path).Comparisons);
    }

    // ── Défaut Critique corrigé en revue ──────────────────────────────────────
    //
    // SubmitVerdict lisait directement _plan[_index] et écrivait, sans jamais vérifier qu'un
    // Next() correspondant avait servi cet index. Un client pouvait donc consigner un verdict
    // pour un item jamais affiché — et, pire, franchir la frontière de quota sans jamais
    // déclencher la pause obligatoire (IsPauseDue n'est consultée que par Next()). Le correctif
    // introduit _servedForIndex, sur le modèle exact d'ElicitationSession (T5) : Next() est seul
    // à le positionner, et seulement quand il sert réellement l'item (pas sur pause/fin).
    [Fact]
    public void Un_verdict_est_refuse_si_next_na_jamais_servi_litem()
    {
        var (session, _) = NewSession();

        // Aucun appel à Next() : le client tente de consigner directement.
        var result = session.SubmitVerdict(JudgeSession.PositionOne, 1);

        Assert.Equal(SessionStatus.Conflict, result.Status);
        Assert.Empty(HumanFile.ReadComparisons(_path).Comparisons);
    }

    [Fact]
    public void Un_verdict_est_refuse_si_next_a_servi_un_autre_item()
    {
        var (session, _) = NewSession();
        session.Next();
        session.SubmitVerdict(JudgeSession.PositionOne, 9000);

        // _index a avancé après la soumission ; _servedForIndex, lui, ne l'a pas suivi tant que
        // Next() n'a pas été rappelé pour le nouvel item courant.
        var result = session.SubmitVerdict(JudgeSession.PositionTwo, 500);

        Assert.Equal(SessionStatus.Conflict, result.Status);
        Assert.Single(HumanFile.ReadComparisons(_path).Comparisons);
    }

    [Fact]
    public void La_frontiere_de_quota_ne_peut_pas_etre_franchie_sans_passer_par_la_pause()
    {
        var (session, _) = NewSession(targetCount: 30, quota: 2, pauseSeconds: 60);

        for (var i = 0; i < 2; i++)
        {
            session.Next();
            session.SubmitVerdict(JudgeSession.PositionOne, 8000);
        }

        // Le client saute délibérément le GET /api/next qui aurait révélé la pause, et tente de
        // consigner un troisième verdict directement : la garde doit l'en empêcher, faute de quoi
        // la page deviendrait le seul rempart de la pause de quota (ce que le brief qualifie de
        // Critique).
        var bypass = session.SubmitVerdict(JudgeSession.PositionOne, 100);

        Assert.Equal(SessionStatus.Conflict, bypass.Status);
        Assert.Equal(2, HumanFile.ReadComparisons(_path).Comparisons.Count);

        // Rappeler Next() correctement révèle bien la pause — la garde n'a pas seulement bloqué
        // l'écriture, elle a aussi laissé la pause faire son travail au prochain passage légitime.
        Assert.True(session.Next().PauseRequired);
    }

    [Fact]
    public void Apres_un_re_jugement_next_reste_requis_avant_le_couple_suivant()
    {
        var (session, _) = NewSession();
        session.Next();
        session.SubmitVerdict(JudgeSession.PositionOne, 9000);
        session.ReJudgeLast(JudgeSession.PositionTwo, 4000);

        // Le re-jugement porte sur le couple déjà servi (_lastJudgedIndex) ; il ne sert jamais le
        // couple suivant. Une soumission directe pour ce couple suivant, sans nouveau Next(),
        // doit donc rester refusée exactement comme avant le re-jugement.
        var result = session.SubmitVerdict(JudgeSession.PositionOne, 1000);

        Assert.Equal(SessionStatus.Conflict, result.Status);
        Assert.Equal(2, HumanFile.ReadComparisons(_path).Comparisons.Count);
    }

    [Fact]
    public void La_session_juge_nexpose_que_trois_operations()
    {
        var methods = typeof(JudgeSession)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // A-6 : aucune échelle absolue n'existe dans l'outil. Aucune API ne permet non plus de
        // sauter un couple.
        Assert.Equal(new[] { "Next", "ReJudgeLast", "SubmitVerdict" }, methods);
    }
}
