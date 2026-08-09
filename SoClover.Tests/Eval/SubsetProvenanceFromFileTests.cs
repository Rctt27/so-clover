using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// <c>SubsetSelector.FromFile</c> est le seul point où la CLI traduit <c>--subset</c> et
/// <c>--subset-outcome</c> en périmètre de mesure. C'est donc là que la provenance doit être
/// produite : <c>ScoreCommand</c> n'a plus qu'à la transmettre, et la garde de saturation ne
/// repose pas sur une ligne de colle non testée.
/// </summary>
public class SubsetProvenanceFromFileTests : IDisposable
{
    private readonly BenchContents _bench = HumanTestData.Bench(boardCount: 40);
    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"subset-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private string WriteElicitation()
    {
        var plan = ElicitationPlan.Build(_bench, seed: 42, targetCount: 40);
        var contents = HumanTestData.Elicitation(_bench, plan, passEvery: 8);

        var path = Path.Combine(_directory, "elicitation.dev.jsonl");
        HumanFile.WriteElicitationManifest(path, contents.Manifest);
        foreach (var line in contents.Elicitations)
            HumanFile.AppendElicitation(path, line);

        return path;
    }

    [Fact]
    public void Le_filtre_demande_est_rendu_tel_quel_comme_provenance()
    {
        var path = WriteElicitation();

        var (_, name, outcome) = SubsetSelector.FromFile(path, "solide");

        Assert.Equal("elicitation.dev.jsonl", name);
        Assert.Equal("solide", outcome);
    }

    // Sans drapeau, le périmètre est « toutes les issues ». L'écrire explicitement supprime
    // l'ambiguïté entre « aucun filtre » et « fichier produit avant l'ajout de la provenance » :
    // le premier est vérifiable, le second ne l'est pas.
    [Fact]
    public void Sans_drapeau_la_provenance_nomme_les_trois_issues_au_lieu_de_rester_nulle()
    {
        var path = WriteElicitation();

        var (_, _, outcome) = SubsetSelector.FromFile(path, outcomeFilter: null);

        Assert.NotNull(outcome);
        Assert.Contains(Outcomes.Pass, outcome);
        Assert.Contains(Outcomes.Solide, outcome);
        Assert.Contains(Outcomes.Tiede, outcome);
    }

    // Un ordre de saisie ne doit pas produire deux provenances différentes pour le même périmètre.
    [Fact]
    public void La_provenance_est_normalisee_independamment_de_l_ordre_saisi()
    {
        var path = WriteElicitation();

        var (_, _, direct) = SubsetSelector.FromFile(path, "solide,tiede");
        var (_, _, reversed) = SubsetSelector.FromFile(path, "tiede, solide");

        Assert.Equal(direct, reversed);
    }
}
