using System.Text;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using Xunit;

namespace SoClover.Tests.Eval;

public class HumanFileTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"human-{Guid.NewGuid():N}.jsonl");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static ElicitationManifest Manifest(int harnessVersion = HumanFile.HarnessVersion) => new(
        Kind: "manifest",
        BenchFile: "eval/boards.dev.jsonl",
        BenchHash: "416b819a41a1",
        Seed: 20260729001,
        TargetCount: 40,
        TimerSeconds: 90,
        QuotaBeforePause: 25,
        CandidatesRunId: "20260728-v5-google-gemma-4-12b-qat-d79a63b9",
        HarnessVersion: harnessVersion,
        CreatedAtUtc: new DateTime(2026, 7, 29, 9, 0, 0, DateTimeKind.Utc));

    private static ElicitationLine Line(
        string boardId = "dev-007", string direction = "Top", string outcome = "solide",
        string? clue = "Pédiatre", int ordinal = 1) => new(
        Kind: "elicitation",
        BoardId: boardId,
        Direction: direction,
        ReferenceWords: ["Chirurgien", "Enfant"],
        Outcome: outcome,
        Clue: clue,
        ElapsedSeconds: 42,
        ServerElapsedSeconds: 45,
        RelationType: "R12_specialisation_croisee",
        RejectedAttempts: ["Hôpitaux"],
        SessionId: "s-1",
        ItemOrdinal: ordinal,
        AuthoredAtUtc: new DateTime(2026, 7, 29, 9, 5, 0, DateTimeKind.Utc));

    [Fact]
    public void Elicitation_fait_un_aller_retour_fidele()
    {
        HumanFile.WriteElicitationManifest(_path, Manifest());
        HumanFile.AppendElicitation(_path, Line());
        HumanFile.AppendAssist(_path, new AssistLine(
            "assist", "dev-007", "Top", null, "candidats tous génériques",
            new DateTime(2026, 7, 29, 9, 6, 0, DateTimeKind.Utc)));

        var read = HumanFile.ReadElicitation(_path);

        Assert.Equal(Manifest(), read.Manifest);

        // Comparaison champ par champ : l'égalité de record sur IReadOnlyList échoue
        // par sémantique reference-based (voir BenchFileTests.cs:32-38 pour le précédent)
        var elicitation = Assert.Single(read.Elicitations);
        var expected = Line();
        Assert.Equal(expected.Kind, elicitation.Kind);
        Assert.Equal(expected.BoardId, elicitation.BoardId);
        Assert.Equal(expected.Direction, elicitation.Direction);
        Assert.Equal(expected.ReferenceWords, elicitation.ReferenceWords);
        Assert.Equal(expected.Outcome, elicitation.Outcome);
        Assert.Equal(expected.Clue, elicitation.Clue);
        Assert.Equal(expected.ElapsedSeconds, elicitation.ElapsedSeconds);
        Assert.Equal(expected.ServerElapsedSeconds, elicitation.ServerElapsedSeconds);
        Assert.Equal(expected.RelationType, elicitation.RelationType);
        Assert.Equal(expected.RejectedAttempts, elicitation.RejectedAttempts);
        Assert.Equal(expected.SessionId, elicitation.SessionId);
        Assert.Equal(expected.ItemOrdinal, elicitation.ItemOrdinal);
        Assert.Equal(expected.AuthoredAtUtc, elicitation.AuthoredAtUtc);

        Assert.Equal("candidats tous génériques", Assert.Single(read.Assists).Notes);
    }

    [Fact]
    public void Une_derniere_ligne_tronquee_est_ignoree_avec_avertissement()
    {
        HumanFile.WriteElicitationManifest(_path, Manifest());
        HumanFile.AppendElicitation(_path, Line());
        File.AppendAllText(_path, "{\"kind\":\"elicitation\",\"boardId\":\"dev-0",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var read = HumanFile.ReadElicitation(_path);

        Assert.Single(read.Elicitations);
    }

    [Fact]
    public void Une_harnessVersion_divergente_est_refusee()
    {
        HumanFile.WriteElicitationManifest(_path, Manifest(harnessVersion: 99));

        var ex = Assert.Throws<HumanIntegrityException>(() => HumanFile.ReadElicitation(_path));
        Assert.Contains("harnessVersion", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Une_ligne_de_kind_inconnu_est_refusee()
    {
        HumanFile.WriteElicitationManifest(_path, Manifest());
        File.AppendAllText(_path, "{\"kind\":\"martien\"}\n{\"kind\":\"martien\"}\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Assert.Throws<HumanIntegrityException>(() => HumanFile.ReadElicitation(_path));
    }

    [Fact]
    public void La_jointure_assist_se_fait_sur_boardId_et_direction()
    {
        HumanFile.WriteElicitationManifest(_path, Manifest());
        HumanFile.AppendElicitation(_path, Line());
        HumanFile.AppendAssist(_path, new AssistLine(
            "assist", "dev-007", "Top", "Pédiatrie", null, DateTime.UtcNow));

        var read = HumanFile.ReadElicitation(_path);

        Assert.Equal("Pédiatrie", HumanFile.AssistFor(read, "dev-007", "Top")!.AssistedClue);
        Assert.Null(HumanFile.AssistFor(read, "dev-007", "Left"));
    }

    [Fact]
    public void Un_comparisonId_rejuge_retient_la_derniere_ligne()
    {
        var manifest = new ComparisonManifest(
            Kind: "manifest",
            BenchFile: "eval/boards.dev.jsonl",
            BenchHash: "416b819a41a1",
            Seed: 20260730001,
            ElicitationFile: "eval/human/elicitation.dev.jsonl",
            Runs: [new ComparisonRunRef("run-a", "eval/runs/a.jsonl")],
            TargetCount: 100,
            QuotaBeforePause: 50,
            HoursSinceElicitation: 26.4,
            EarlyStart: false,
            HarnessVersion: HumanFile.HarnessVersion,
            CreatedAtUtc: new DateTime(2026, 7, 30, 9, 0, 0, DateTimeKind.Utc));

        ComparisonLine Comparison(string verdict) => new(
            Kind: "comparison",
            ComparisonId: "c-0123456789ab",
            Family: ComparisonFamilies.HumanVsModel,
            BoardId: "dev-007",
            Direction: "Top",
            ReferenceWords: ["Chirurgien", "Enfant"],
            OptionA: new ComparisonOption(ComparisonSources.Human, null, "Pédiatre"),
            OptionB: new ComparisonOption(ComparisonSources.Model, "run-a", "Hôpital"),
            PresentedOrder: PresentedOrders.Ab,
            Verdict: verdict,
            ElapsedMs: 11200,
            DuplicateOf: null,
            SessionId: "s-2",
            ItemOrdinal: 31,
            JudgedAtUtc: new DateTime(2026, 7, 30, 9, 5, 0, DateTimeKind.Utc));

        HumanFile.WriteComparisonManifest(_path, manifest);
        HumanFile.AppendComparison(_path, Comparison("A"));
        HumanFile.AppendComparison(_path, Comparison("B"));

        var read = HumanFile.ReadComparisons(_path);

        Assert.Equal(2, read.Comparisons.Count);
        Assert.Equal("B", HumanFile.LatestByComparisonId(read)["c-0123456789ab"].Verdict);
    }

    [Fact]
    public void Le_vocabulaire_des_relations_est_ferme_et_contient_les_quatorze_valeurs()
    {
        Assert.Equal(14, RelationTypes.All.Count);
        Assert.True(RelationTypes.IsKnown("R1_categorie"));
        Assert.True(RelationTypes.IsKnown("R12_specialisation_croisee"));
        Assert.True(RelationTypes.IsKnown("R13_expression_figee"));
        Assert.True(RelationTypes.IsKnown("autre"));
        Assert.False(RelationTypes.IsKnown("R14_inventee"));
        Assert.False(RelationTypes.IsKnown("r1_categorie"));
    }
}
