using SoClover.Eval;
using SoClover.Eval.Human;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Trou de couverture comblé en revue (MINEUR 5) : la persistance de <c>EarlyStart</c> et
/// <c>HoursSinceElicitation</c> dans le manifeste lors d'un contournement de la garde A-5 n'était
/// vérifiée que par une manipulation manuelle du verbe <c>judge</c>, non reproductible. Un
/// contournement tracé dans l'artefact est une propriété centrale du protocole B : « earlyStart
/// devient une donnée du corpus, pas un secret » n'a de sens que si le manifeste écrit le porte
/// réellement. <see cref="EvalProgram.BuildComparisonManifest"/> isole exactement le calcul qui
/// vivait en ligne dans <c>EvalProgram.Judge</c> (Program.cs), rendant ce fait vérifiable sans
/// démarrer de serveur HTTP ni attendre un arrêt de processus.
/// </summary>
public class JudgeManifestTests
{
    private static readonly IReadOnlyList<ComparisonRunRef> Runs =
    [
        new ComparisonRunRef("run-a", "eval/runs/run-a.jsonl"),
        new ComparisonRunRef("run-b", "eval/runs/run-b.jsonl"),
    ];

    [Fact]
    public void Le_contournement_de_la_garde_A5_est_trace_dans_le_manifeste_ecrit()
    {
        var guard = new JudgeGuardResult(Allowed: true, HoursSinceElicitation: 3.2, EarlyStart: true);

        var manifest = EvalProgram.BuildComparisonManifest(
            guard,
            benchFile: "eval/boards.dev.jsonl",
            benchHash: "416b819a41a1",
            seed: 20260730001,
            elicitationFile: "eval/human/elicitation.dev.jsonl",
            runs: Runs,
            targetCount: 100,
            quotaBeforePause: 50,
            createdAtUtc: new DateTime(2026, 7, 30, 8, 0, 0, DateTimeKind.Utc));

        Assert.True(manifest.EarlyStart);
        Assert.Equal(3.2, manifest.HoursSinceElicitation);
    }

    [Fact]
    public void Une_seance_demarree_naturellement_ne_porte_aucune_marque()
    {
        var guard = new JudgeGuardResult(Allowed: true, HoursSinceElicitation: 26.4, EarlyStart: false);

        var manifest = EvalProgram.BuildComparisonManifest(
            guard,
            benchFile: "eval/boards.dev.jsonl",
            benchHash: "416b819a41a1",
            seed: 20260730001,
            elicitationFile: "eval/human/elicitation.dev.jsonl",
            runs: Runs,
            targetCount: 100,
            quotaBeforePause: 50,
            createdAtUtc: new DateTime(2026, 7, 30, 8, 0, 0, DateTimeKind.Utc));

        Assert.False(manifest.EarlyStart);
        Assert.Equal(26.4, manifest.HoursSinceElicitation);
    }

    [Fact]
    public void Les_autres_champs_du_manifeste_sont_repris_tels_quels()
    {
        var guard = new JudgeGuardResult(Allowed: true, HoursSinceElicitation: 26.4, EarlyStart: false);

        var manifest = EvalProgram.BuildComparisonManifest(
            guard,
            benchFile: "eval/boards.dev.jsonl",
            benchHash: "416b819a41a1",
            seed: 20260730001,
            elicitationFile: "eval/human/elicitation.dev.jsonl",
            runs: Runs,
            targetCount: 100,
            quotaBeforePause: 50,
            createdAtUtc: new DateTime(2026, 7, 30, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal("manifest", manifest.Kind);
        Assert.Equal("eval/boards.dev.jsonl", manifest.BenchFile);
        Assert.Equal("416b819a41a1", manifest.BenchHash);
        Assert.Equal(20260730001, manifest.Seed);
        Assert.Equal("eval/human/elicitation.dev.jsonl", manifest.ElicitationFile);
        Assert.Equal(100, manifest.TargetCount);
        Assert.Equal(50, manifest.QuotaBeforePause);
        Assert.Equal(new DateTime(2026, 7, 30, 8, 0, 0, DateTimeKind.Utc), manifest.CreatedAtUtc);

        // Arbitrage du cycle : ne jamais comparer un record entier portant une collection —
        // comparaison champ par champ, sans en omettre aucun.
        Assert.Equal(Runs.Count, manifest.Runs.Count);
        for (var i = 0; i < Runs.Count; i++)
        {
            Assert.Equal(Runs[i].RunId, manifest.Runs[i].RunId);
            Assert.Equal(Runs[i].File, manifest.Runs[i].File);
        }
    }
}
