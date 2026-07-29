using System.Reflection;
using SoClover.Domain.Validation;
using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Runner;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class ElicitationSessionTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"elicit-{Guid.NewGuid():N}.jsonl");
    private readonly BenchContents _bench = HumanTestData.Bench(boardCount: 10);
    private DateTime _now = new(2026, 7, 29, 9, 0, 0, DateTimeKind.Utc);

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private ElicitationSession NewSession(
        int targetCount = 4, int quota = 25, int pauseSeconds = 300,
        RunContents? candidatesRun = null, ElicitationContents? existing = null)
    {
        var plan = ElicitationPlan.Build(_bench, seed: 42, targetCount);

        if (existing is null)
        {
            HumanFile.WriteElicitationManifest(_path, HumanTestData.ElicitationManifest(
                _bench.Manifest.BenchHash, targetCount, quota));
            existing = HumanFile.ReadElicitation(_path);
        }

        return new ElicitationSession(
            _bench, plan, existing,
            ElicitationSession.BuildCandidateIndex(candidatesRun),
            new FrenchOffClueValidator(),
            _path, sessionId: "s-test",
            timerSeconds: 90, quotaBeforePause: quota, pauseSeconds: pauseSeconds,
            clock: () => _now);
    }

    [Fact]
    public void Les_candidats_du_modele_sont_verrouilles_tant_quaucune_tentative_nest_consignee()
    {
        var session = NewSession();
        var item = session.Next();

        Assert.Equal(SessionStatus.Conflict, session.Candidates().Status);

        Assert.Equal(SessionStatus.Ok,
            session.SubmitAttempt("Pédiatre", Outcomes.Solide, "R12_specialisation_croisee", 42).Status);

        Assert.Equal(SessionStatus.Ok, session.Candidates().Status);
        Assert.NotNull(item.BoardId);
    }

    [Fact]
    public void Un_pass_est_une_mesure_consignee_avec_son_chrono()
    {
        var session = NewSession();
        session.Next();

        Assert.Equal(SessionStatus.Ok, session.SubmitAttempt(null, Outcomes.Pass, null, 88).Status);

        var line = Assert.Single(HumanFile.ReadElicitation(_path).Elicitations);
        Assert.Equal(Outcomes.Pass, line.Outcome);
        Assert.Null(line.Clue);
        Assert.Equal(88, line.ElapsedSeconds);
        Assert.Equal(1, line.ItemOrdinal);
    }

    [Fact]
    public void Un_indice_illegal_est_refuse_avec_sa_regle_et_consigne_dans_rejectedAttempts()
    {
        var session = NewSession();
        var item = session.Next();
        var boardWord = _bench.Boards.Single(b => b.BoardId == item.BoardId).Cards[0][0];

        var rejected = session.SubmitAttempt(boardWord, Outcomes.Solide, null, 12);

        Assert.Equal(SessionStatus.Rejected, rejected.Status);
        Assert.Contains(nameof(ClueValidationRule.ExactMatch), rejected.RejectionRules);
        Assert.Empty(HumanFile.ReadElicitation(_path).Elicitations);

        Assert.Equal(SessionStatus.Ok, session.SubmitAttempt("Pédiatre", Outcomes.Solide, null, 30).Status);

        var line = Assert.Single(HumanFile.ReadElicitation(_path).Elicitations);
        Assert.Equal([boardWord], line.RejectedAttempts);
    }

    [Fact]
    public void Un_relationType_hors_vocabulaire_est_rejete_et_rien_nest_consigne()
    {
        var session = NewSession();
        session.Next();

        var result = session.SubmitAttempt("Pédiatre", Outcomes.Solide, "R99_inventee", 10);

        Assert.Equal(SessionStatus.BadRequest, result.Status);
        Assert.Empty(HumanFile.ReadElicitation(_path).Elicitations);
    }

    [Fact]
    public void Le_quota_declenche_une_pause_qui_ne_se_quitte_quapres_le_delai()
    {
        var session = NewSession(targetCount: 6, quota: 2, pauseSeconds: 300);

        for (var i = 0; i < 2; i++)
        {
            session.Next();
            session.SubmitAttempt("Pédiatre", Outcomes.Solide, null, 10);
            session.RecordAssist(null, null);
        }

        var paused = session.Next();
        Assert.True(paused.PauseRequired);
        Assert.Null(paused.BoardId);
        Assert.Equal(300, paused.PauseSecondsRemaining);

        _now = _now.AddSeconds(120);
        Assert.True(session.Next().PauseRequired);

        _now = _now.AddSeconds(200);
        var resumed = session.Next();
        Assert.False(resumed.PauseRequired);
        Assert.NotNull(resumed.BoardId);
    }

    [Fact]
    public void La_reprise_repart_a_litem_non_saisi_et_litemOrdinal_reste_monotone()
    {
        var first = NewSession(targetCount: 4);
        first.Next();
        first.SubmitAttempt("Pédiatre", Outcomes.Solide, null, 10);
        first.RecordAssist(null, null);
        first.Next();
        first.SubmitAttempt("Radiologue", Outcomes.Tiede, null, 20);

        var resumed = NewSession(targetCount: 4, existing: HumanFile.ReadElicitation(_path));
        var item = resumed.Next();

        Assert.Equal(2, resumed.CompletedCount);
        Assert.False(item.AwaitingReveal);

        resumed.SubmitAttempt("Ambulancier", Outcomes.Solide, null, 15);

        var ordinals = HumanFile.ReadElicitation(_path).Elicitations.Select(e => e.ItemOrdinal);
        Assert.Equal([1, 2, 3], ordinals);
    }

    [Fact]
    public void Le_chrono_serveur_est_mesure_entre_next_et_attempt()
    {
        var session = NewSession();
        session.Next();
        _now = _now.AddSeconds(57);

        session.SubmitAttempt("Pédiatre", Outcomes.Solide, null, 42);

        var line = Assert.Single(HumanFile.ReadElicitation(_path).Elicitations);
        Assert.Equal(42, line.ElapsedSeconds);
        Assert.Equal(57, line.ServerElapsedSeconds);
    }

    [Fact]
    public void La_ligne_assist_nest_ecrite_que_si_elle_porte_quelque_chose()
    {
        var session = NewSession();
        session.Next();
        session.SubmitAttempt("Pédiatre", Outcomes.Solide, null, 10);

        session.RecordAssist(null, null);
        Assert.Empty(HumanFile.ReadElicitation(_path).Assists);

        session.Next();
        session.SubmitAttempt("Radiologue", Outcomes.Solide, null, 10);
        session.RecordAssist("Radiographie", "meilleur après lecture des candidats");

        Assert.Equal("Radiographie", Assert.Single(HumanFile.ReadElicitation(_path).Assists).AssistedClue);
    }

    [Fact]
    public void Aucune_API_de_saut_nexiste_sur_la_session()
    {
        var methods = typeof(ElicitationSession)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // A-1 : un item ne se quitte que par solide, tiede ou pass. Ajouter une méthode ici
        // sans réfléchir à A-1 fait échouer ce test — c'est exactement son rôle.
        Assert.Equal(new[] { "Candidates", "Next", "RecordAssist", "SubmitAttempt" }, methods);
    }

    [Fact]
    public void Les_candidats_rendus_sont_ceux_du_run_de_candidats()
    {
        var session = NewSession(candidatesRun: null);
        var item = session.Next();
        session.SubmitAttempt("Pédiatre", Outcomes.Solide, null, 10);

        var empty = session.Candidates();
        Assert.Equal(SessionStatus.Ok, empty.Status);
        Assert.Null(empty.Clue);
        Assert.Empty(empty.Candidates);
        Assert.NotNull(item.TargetCells);
        Assert.Equal(2, item.TargetCells!.Count);
    }

    // ── Corrections suite à revue : garde Next() avant SubmitAttempt ──────────

    [Fact]
    public void Une_soumission_sans_next_prealable_est_refusee_et_rien_nest_consigne()
    {
        var session = NewSession();

        // Aucun appel à Next() : _servedForIndex (-1) ne peut pas correspondre à _index (0).
        // Sans la garde, ceci écrirait une ligne avec serverElapsedSeconds == elapsedSeconds
        // (aucune mesure serveur indépendante — exactement ce que le PRD veut détecter).
        var result = session.SubmitAttempt("Pédiatre", Outcomes.Solide, null, 10);

        Assert.Equal(SessionStatus.Conflict, result.Status);
        Assert.Empty(HumanFile.ReadElicitation(_path).Elicitations);
    }

    [Fact]
    public void Une_soumission_apres_avancement_sans_rappeler_next_est_refusee_chrono_perime()
    {
        var session = NewSession(targetCount: 4);

        session.Next();
        Assert.Equal(SessionStatus.Ok, session.SubmitAttempt("Pédiatre", Outcomes.Solide, null, 10).Status);
        Assert.Equal(SessionStatus.Ok, session.RecordAssist(null, null).Status);

        // RecordAssist a fait avancer _index vers le deuxième item, mais Next() n'a jamais été
        // rappelé pour lui : _servedForIndex pointe encore sur le premier item. Sans la garde,
        // ceci consignerait une ligne pour le deuxième item avec un serverElapsedSeconds mesuré
        // depuis le service du PREMIER — une donnée fausse, sans aucun signal d'erreur.
        var stale = session.SubmitAttempt("Radiologue", Outcomes.Solide, null, 20);

        Assert.Equal(SessionStatus.Conflict, stale.Status);
        Assert.Single(HumanFile.ReadElicitation(_path).Elicitations);
    }

    [Fact]
    public void Une_deuxieme_frontiere_de_quota_redeclenche_la_pause()
    {
        var session = NewSession(targetCount: 8, quota: 2, pauseSeconds: 300);

        for (var i = 0; i < 2; i++)
        {
            session.Next();
            session.SubmitAttempt("Pédiatre", Outcomes.Solide, null, 10);
            session.RecordAssist(null, null);
        }

        Assert.True(session.Next().PauseRequired);
        _now = _now.AddSeconds(300);
        Assert.False(session.Next().PauseRequired);

        for (var i = 0; i < 2; i++)
        {
            session.Next();
            session.SubmitAttempt("Pédiatre", Outcomes.Solide, null, 10);
            session.RecordAssist(null, null);
        }

        // Deuxième frontière : le compteur de complétion atteint de nouveau un multiple du
        // quota (4). _pauseAcknowledgedAt ne doit plus correspondre à ce nouveau compte, donc
        // la pause doit se redéclencher — et non rester acquittée à vie après la première.
        var secondPause = session.Next();
        Assert.True(secondPause.PauseRequired);
        Assert.Equal(300, secondPause.PauseSecondsRemaining);
    }
}
