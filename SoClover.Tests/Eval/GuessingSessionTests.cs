using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Séance D « devineur ». La session applique le protocole — l'opérateur ne peut pas le
/// contourner : pas d'API de saut, <c>Next()</c> obligatoire avant toute soumission, et la vue ne
/// porte ni paire de référence ni identifiant de board.
/// </summary>
public class GuessingSessionTests : IDisposable
{
    private const long Seed = 20260807001;

    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"guessing-{Guid.NewGuid():N}.jsonl");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        GC.SuppressFinalize(this);
    }

    private GuessingSession Build(int boardCount = 3, GuessingContents? existing = null)
    {
        var bench = HumanTestData.Bench(boardCount);
        var run = HumanTestData.Run(bench, "run-a", "gemma");
        var plan = GuessingPlan.Build(bench, run, new HashSet<string>(), Seed);

        // Le manifeste est la ligne 1 du fichier — c'est le verbe CLI qui l'écrit au démarrage
        // d'une séance neuve. Une reprise le trouve déjà là et ne doit surtout pas l'écraser.
        if (!File.Exists(_path))
            HumanFile.WriteGuessingManifest(_path, EmptyContents().Manifest);

        return new GuessingSession(
            bench, plan,
            existing ?? EmptyContents(),
            _path, "s-test", () => new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc));
    }

    private static GuessingContents EmptyContents() =>
        new(new GuessingManifest("manifest", "eval/boards.dev.jsonl", "aaaaaaaaaaaa", Seed,
            "run-a", "eval/runs/run-a.jsonl", null, [], 12, 1,
            new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc)), []);

    [Fact]
    public void La_vue_presente_seize_mots_et_un_indice()
    {
        var view = Build().Next();

        Assert.False(view.Finished);
        Assert.NotNull(view.PresentedWords);
        Assert.Equal(16, view.PresentedWords!.Count);
        Assert.False(string.IsNullOrWhiteSpace(view.Clue));
    }

    /// <summary>
    /// Aveuglement structurel, au même titre que <c>JudgeItemView</c> : la vue ne peut pas laisser
    /// fuiter la réponse. Le <c>boardId</c> en est absent aussi — l'afficher permettrait de
    /// reconnaître un board déjà vu et d'exploiter la mémoire que la dispersion cherche justement
    /// à refroidir.
    /// </summary>
    [Fact]
    public void La_vue_ne_porte_ni_paire_de_reference_ni_board_id()
    {
        var view = Build().Next();

        var properties = view.GetType().GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("ReferenceWords", properties);
        Assert.DoesNotContain("BoardId", properties);
    }

    [Fact]
    public void Lordre_de_presentation_est_celui_du_decodage_zero()
    {
        var bench = HumanTestData.Bench(3);
        var view = Build().Next();

        var board = bench.Boards.Single(b =>
            SoClover.Eval.Bench.BenchBoardMapper.AllWords(b)
                .OrderBy(w => w, StringComparer.Ordinal)
                .SequenceEqual(view.PresentedWords!.OrderBy(w => w, StringComparer.Ordinal)));

        var attendu = SoClover.Eval.Decoder.ShuffleSeed.Shuffle(
            SoClover.Eval.Bench.BenchBoardMapper.AllWords(board),
            SoClover.Eval.Decoder.ShuffleSeed.ForClue("aaaaaaaaaaaa", board.BoardId, 0));

        Assert.Equal(attendu, view.PresentedWords);
    }

    [Fact]
    public void Une_soumission_sans_next_est_refusee()
    {
        var session = Build();
        var view = session.Next();
        session.SubmitGuess([view.PresentedWords![0], view.PresentedWords[1]], 1000);

        // Le deuxième item n'a jamais été servi : la soumission doit être bloquée.
        var second = session.SubmitGuess([view.PresentedWords[0], view.PresentedWords[1]], 1000);

        Assert.Equal(SessionStatus.Conflict, second.Status);
    }

    [Fact]
    public void Deux_mots_identiques_sont_refuses()
    {
        var session = Build();
        var view = session.Next();

        var result = session.SubmitGuess([view.PresentedWords![0], view.PresentedWords[0]], 1000);

        Assert.Equal(SessionStatus.BadRequest, result.Status);
    }

    [Fact]
    public void Un_mot_hors_plateau_est_refuse()
    {
        var session = Build();
        var view = session.Next();

        var result = session.SubmitGuess([view.PresentedWords![0], "mot-inexistant"], 1000);

        Assert.Equal(SessionStatus.BadRequest, result.Status);
    }

    [Fact]
    public void Autre_quun_couple_de_mots_est_refuse()
    {
        var session = Build();
        var view = session.Next();

        Assert.Equal(SessionStatus.BadRequest, session.SubmitGuess([view.PresentedWords![0]], 1000).Status);
    }

    [Fact]
    public void Le_r_vaut_un_quand_la_paire_est_retrouvee()
    {
        var bench = HumanTestData.Bench(3);
        var session = Build();
        session.Next();

        // La paire de référence de l'item courant, lue depuis l'oracle gelé du banc.
        var item = GuessingPlan.Build(
            bench, HumanTestData.Run(bench, "run-a", "gemma"), new HashSet<string>(), Seed)[0];
        var reference = SoClover.Eval.Bench.BenchBoardMapper.ReferenceWords(
            bench.Boards.Single(b => b.BoardId == item.BoardId),
            Enum.Parse<SoClover.Domain.Direction>(item.Direction));

        session.SubmitGuess([reference[0], reference[1]], 1000);

        Assert.Equal(1.0, HumanFile.ReadGuessing(_path).Guesses.Single().R);
    }

    [Fact]
    public void Le_r_vaut_un_demi_quand_un_seul_mot_est_bon()
    {
        var bench = HumanTestData.Bench(3);
        var session = Build();
        var view = session.Next();

        var item = GuessingPlan.Build(
            bench, HumanTestData.Run(bench, "run-a", "gemma"), new HashSet<string>(), Seed)[0];
        var reference = SoClover.Eval.Bench.BenchBoardMapper.ReferenceWords(
            bench.Boards.Single(b => b.BoardId == item.BoardId),
            Enum.Parse<SoClover.Domain.Direction>(item.Direction));
        var intrus = view.PresentedWords!.First(w => !reference.Contains(w));

        session.SubmitGuess([reference[0], intrus], 1000);

        Assert.Equal(0.5, HumanFile.ReadGuessing(_path).Guesses.Single().R);
    }

    [Fact]
    public void La_reprise_saute_les_directions_deja_devinees()
    {
        var session = Build();
        var view = session.Next();
        session.SubmitGuess([view.PresentedWords![0], view.PresentedWords[1]], 1000);

        var reprise = Build(existing: HumanFile.ReadGuessing(_path));

        Assert.Equal(1, reprise.CompletedCount);
        Assert.NotEqual(view.Clue, reprise.Next().Clue);
    }

    [Fact]
    public void La_seance_se_termine_quand_toutes_les_directions_sont_devinees()
    {
        var session = Build(boardCount: 1);

        for (var i = 0; i < 4; i++)
        {
            var view = session.Next();
            Assert.False(view.Finished);
            session.SubmitGuess([view.PresentedWords![0], view.PresentedWords[1]], 1000);
        }

        Assert.True(session.Next().Finished);
    }
}
