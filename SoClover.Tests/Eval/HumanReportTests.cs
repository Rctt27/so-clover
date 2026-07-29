using SoClover.Eval.Human;
using Xunit;

namespace SoClover.Tests.Eval;

public class HumanReportTests
{
    private static ElicitationLine Line(
        string boardId, string outcome, string? clue, int elapsed, int ordinal,
        string? relation = "R1_categorie", string[]? rejected = null) => new(
        Kind: "elicitation", BoardId: boardId, Direction: "Top",
        ReferenceWords: ["a", "b"], Outcome: outcome, Clue: clue,
        ElapsedSeconds: elapsed, ServerElapsedSeconds: elapsed + 2,
        RelationType: relation, RejectedAttempts: rejected ?? [],
        SessionId: "s", ItemOrdinal: ordinal,
        AuthoredAtUtc: new DateTime(2026, 7, 29, 10, ordinal, 0, DateTimeKind.Utc));

    private static ComparisonLine Comparison(
        string id, string family, string verdict, string presentedOrder,
        string sourceA = ComparisonSources.Human, string sourceB = ComparisonSources.Model,
        string? duplicateOf = null, string boardId = "dev-001") => new(
        Kind: "comparison", ComparisonId: id, Family: family, BoardId: boardId, Direction: "Top",
        ReferenceWords: ["a", "b"],
        OptionA: new ComparisonOption(sourceA, null, "indiceA"),
        OptionB: new ComparisonOption(sourceB, "run-a", "indiceB"),
        PresentedOrder: presentedOrder, Verdict: verdict, ElapsedMs: 9000,
        DuplicateOf: duplicateOf, SessionId: "s", ItemOrdinal: 1,
        JudgedAtUtc: new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc));

    private static ElicitationContents Elicitation(params ElicitationLine[] lines) =>
        new(new ElicitationManifest("manifest", "b.jsonl", "hhh", 1, TargetCount: 5,
                TimerSeconds: 90, QuotaBeforePause: 25, CandidatesRunId: null,
                HarnessVersion: 1, CreatedAtUtc: DateTime.UtcNow),
            lines, []);

    private static ComparisonContents Comparisons(params ComparisonLine[] lines) =>
        new(new ComparisonManifest("manifest", "b.jsonl", "hhh", 1, "e.jsonl", [],
                TargetCount: 10, QuotaBeforePause: 50, HoursSinceElicitation: 26,
                EarlyStart: false, HarnessVersion: 1, CreatedAtUtc: DateTime.UtcNow),
            lines);

    [Fact]
    public void Le_taux_de_pass_et_la_mediane_par_issue_sont_rendus()
    {
        var report = HumanReport.ForElicitation(Elicitation(
            Line("dev-001", Outcomes.Solide, "x", 10, 1),
            Line("dev-002", Outcomes.Solide, "y", 30, 2),
            Line("dev-003", Outcomes.Solide, "z", 50, 3),
            Line("dev-004", Outcomes.Pass, null, 88, 4, relation: null)));

        Assert.Equal(4, report.ItemsSaved);
        Assert.Equal(5, report.ItemsPlanned);
        Assert.Equal(0.25, report.PassRate);

        var solide = report.Elapsed.Single(e => e.Outcome == Outcomes.Solide);
        Assert.Equal(30, solide.MedianSeconds);
        Assert.Equal(10, solide.MinSeconds);
        Assert.Equal(50, solide.MaxSeconds);
    }

    [Fact]
    public void Les_indices_refuses_et_les_relations_sont_comptes()
    {
        var report = HumanReport.ForElicitation(Elicitation(
            Line("dev-001", Outcomes.Solide, "x", 10, 1, relation: "R1_categorie", rejected: ["a", "b"]),
            Line("dev-002", Outcomes.Tiede, "y", 20, 2, relation: "R1_categorie"),
            Line("dev-003", Outcomes.Solide, "z", 30, 3, relation: "R12_specialisation_croisee")));

        // RejectedClueCount compte les TENTATIVES refusées (2 sur la première ligne), pas les
        // items qui en ont eu.
        Assert.Equal(2, report.RejectedClueCount);
        Assert.Equal(2, report.Relations.Single(r => r.RelationType == "R1_categorie").Count);
    }

    [Fact]
    public void Le_taux_de_victoire_de_la_position_1_est_calcule_hors_egalites()
    {
        // A gagne en AB → position 1 ; B gagne en AB → position 2 ;
        // A gagne en BA → position 2 ; B gagne en BA → position 1.
        var report = HumanReport.ForComparisons(Comparisons(
            Comparison("c-1", ComparisonFamilies.HumanVsModel, "A", PresentedOrders.Ab),
            Comparison("c-2", ComparisonFamilies.HumanVsModel, "B", PresentedOrders.Ba),
            Comparison("c-3", ComparisonFamilies.HumanVsModel, "B", PresentedOrders.Ab),
            Comparison("c-4", ComparisonFamilies.HumanVsModel, "tie", PresentedOrders.Ab)));

        Assert.Equal(4, report.ItemsJudged);
        Assert.Equal(2.0 / 3.0, report.Position1WinRate, 6);
        Assert.Equal(0.25, report.TieRate);
    }

    [Fact]
    public void Un_lot_dont_la_position_1_gagne_trop_souvent_est_declare_suspect()
    {
        var lines = Enumerable.Range(0, 10)
            .Select(i => Comparison($"c-{i}", ComparisonFamilies.HumanVsModel, "A", PresentedOrders.Ab))
            .ToArray();

        var report = HumanReport.ForComparisons(Comparisons(lines));

        Assert.Equal(1.0, report.Position1WinRate);
        Assert.True(report.Position1Suspect);
    }

    [Fact]
    public void La_coherence_intra_juge_se_lit_sur_les_doublons_inverses()
    {
        var report = HumanReport.ForComparisons(Comparisons(
            Comparison("c-1", ComparisonFamilies.HumanVsModel, "A", PresentedOrders.Ab),
            Comparison("c-1" + ComparisonPlan.DuplicateSuffix, ComparisonFamilies.HumanVsModel,
                "A", PresentedOrders.Ba, duplicateOf: "c-1"),
            Comparison("c-2", ComparisonFamilies.HumanVsModel, "A", PresentedOrders.Ab, boardId: "dev-002"),
            Comparison("c-2" + ComparisonPlan.DuplicateSuffix, ComparisonFamilies.HumanVsModel,
                "B", PresentedOrders.Ba, duplicateOf: "c-2", boardId: "dev-002")));

        Assert.Equal(2, report.DuplicatePairCount);
        Assert.Equal(0.5, report.IntraJudgeAgreement);
    }

    [Fact]
    public void Les_ancres_ratees_rendent_le_lot_suspect()
    {
        // Ancre réussie = le verdict désigne l'option modèle, jamais l'aléatoire.
        var report = HumanReport.ForComparisons(Comparisons(
            Comparison("a-1", ComparisonFamilies.Anchor, "A", PresentedOrders.Ab,
                ComparisonSources.Model, ComparisonSources.Random),
            Comparison("a-2", ComparisonFamilies.Anchor, "B", PresentedOrders.Ab,
                ComparisonSources.Model, ComparisonSources.Random, boardId: "dev-002"),
            Comparison("a-3", ComparisonFamilies.Anchor, "B", PresentedOrders.Ba,
                ComparisonSources.Model, ComparisonSources.Random, boardId: "dev-003")));

        Assert.Equal(3, report.AnchorCount);
        Assert.Equal(1, report.AnchorCorrect);
        Assert.True(report.AnchorSuspect);
    }

    [Fact]
    public void Le_dernier_verdict_dun_comparisonId_est_le_seul_compte()
    {
        var report = HumanReport.ForComparisons(Comparisons(
            Comparison("c-1", ComparisonFamilies.HumanVsModel, "A", PresentedOrders.Ab),
            Comparison("c-1", ComparisonFamilies.HumanVsModel, "tie", PresentedOrders.Ab)));

        Assert.Equal(1, report.ItemsJudged);
        Assert.Equal(1.0, report.TieRate);
    }
}
