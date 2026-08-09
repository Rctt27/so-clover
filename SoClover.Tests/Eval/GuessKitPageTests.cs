using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Web;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Le kit de la séance E est une <b>substitution</b>, pas une réécriture. Ces tests sont la seule
/// garantie que H2 devine sur la page de H1 : ils comparent le kit à <c>guess.html</c> hors de la
/// région de transport, et vérifient que rien de ce que le serveur cachait ne fuite dans un
/// fichier qui, lui, part par courriel.
/// </summary>
public class GuessKitPageTests
{
    private static string RealPage =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Web", "Pages", "guess.html"));

    private static (BenchContents Bench, IReadOnlyList<GuessPlanItem> Plan) Fixture(int boardCount = 6)
    {
        var bench = HumanTestData.Bench(boardCount);
        var plan = GuessingPlan.Build(
            bench, HumanTestData.Run(bench, "run-a", "gemma"), new HashSet<string>(), seed: 20260807001);
        return (bench, plan);
    }

    private static GuessKitPayload Payload(int boardCount = 6)
    {
        var (bench, plan) = Fixture(boardCount);
        return GuessKit.BuildPayload(bench, plan, Manifest(bench), new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc));
    }

    private static GuessingManifest Manifest(BenchContents bench) => new(
        "manifest", "eval/boards.dev.jsonl", bench.Manifest.BenchHash, 20260807001,
        "run-a", "eval/runs/run-a.jsonl", null, [], 0, HumanFile.HarnessVersion,
        new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc));

    // ── Invariance de présentation ──────────────────────────────────────────

    /// <summary>
    /// La garde 1 rendue exécutable : entre la séance D et la séance E, la seule variable doit être
    /// la personne. Tout ce qui précède le marqueur d'ouverture et tout ce qui suit le marqueur de
    /// fermeture — donc le CSS, <c>render()</c>, <c>toggle()</c> et la touche Entrée — est recopié
    /// octet pour octet.
    /// </summary>
    [Fact]
    public void Tout_ce_qui_est_hors_marqueurs_est_identique_a_la_page_servie()
    {
        var page = RealPage;
        var (head, tail) = GuessKitPage.SplitTransport(page);
        var kit = GuessKitPage.Build(page, Payload());

        Assert.StartsWith(head, kit, StringComparison.Ordinal);
        Assert.EndsWith(tail, kit, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sans cette vérification, le test précédent pourrait rester vert alors que les marqueurs
    /// auraient glissé et ne borneraient plus que du vide : ce qui compte doit être DANS les
    /// moitiés invariantes.
    /// </summary>
    [Fact]
    public void Les_moities_invariantes_couvrent_bien_le_rendu_et_le_style()
    {
        var (head, tail) = GuessKitPage.SplitTransport(RealPage);

        Assert.Contains("--target:#f0b429", head, StringComparison.Ordinal);
        Assert.Contains(".word.picked", head, StringComparison.Ordinal);
        Assert.Contains("function render()", tail, StringComparison.Ordinal);
        Assert.Contains("function toggle(word)", tail, StringComparison.Ordinal);
        Assert.Contains("event.key === \"Enter\"", tail, StringComparison.Ordinal);
    }

    // ── Aveuglement ─────────────────────────────────────────────────────────

    /// <summary>
    /// Ce que <c>GuessServerTests</c> garantit sur le fil, il faut le garantir ici <b>dans un
    /// fichier</b> — le kit quitte la machine, il ne peut pas révéler ce qu'il ne contient pas.
    /// Les seize mots y sont, évidemment : ce sont les candidats. Ce qui doit être absent, c'est la
    /// désignation de la paire visée, et l'identité du plateau.
    /// </summary>
    [Fact]
    public void Le_kit_ne_porte_ni_paire_de_reference_ni_identifiant_de_plateau()
    {
        var (bench, plan) = Fixture();

        // Les indices de HumanTestData embarquent le boardId (« gemma-dev-003-Bottom ») : c'est un
        // artefact de la fixture, pas du kit. On les neutralise pour que l'assertion porte sur la
        // STRUCTURE du kit, seule chose que le harnais contrôle.
        var neutre = plan.Select(i => new GuessPlanItem(i.BoardId, i.Direction, "indice")).ToList();
        var kit = GuessKitPage.Build(
            RealPage,
            GuessKit.BuildPayload(bench, neutre, Manifest(bench), new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc)));

        Assert.DoesNotContain("referenceWords", kit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dev-", kit, StringComparison.Ordinal);
        Assert.DoesNotContain("\"direction\"", kit, StringComparison.Ordinal);
    }

    /// <summary>
    /// « Hors ligne » doit être une propriété du fichier, pas une consigne à l'ami : la seule
    /// occurrence de <c>fetch(</c> de la page vivait dans la région substituée.
    /// </summary>
    [Fact]
    public void Le_kit_ne_contient_aucun_acces_reseau()
    {
        var kit = GuessKitPage.Build(RealPage, Payload());

        Assert.Contains("fetch(", RealPage, StringComparison.Ordinal);
        Assert.DoesNotContain("fetch(", kit, StringComparison.Ordinal);
        Assert.DoesNotContain("XMLHttpRequest", kit, StringComparison.Ordinal);
    }

    /// <summary>
    /// Le score n'est jamais calculé dans la page : sans quoi la paire de référence devrait y être,
    /// sous une forme ou une autre.
    /// </summary>
    [Fact]
    public void Le_kit_ne_score_pas()
    {
        var kit = GuessKitPage.Build(RealPage, Payload());

        Assert.DoesNotContain("recovery", kit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("le score est calculé à l'import", kit, StringComparison.Ordinal);
    }

    // ── Charge utile ────────────────────────────────────────────────────────

    [Fact]
    public void La_charge_utile_est_gravee_avec_ses_quatre_champs_d_identite()
    {
        var payload = Payload();
        var kit = GuessKitPage.Build(RealPage, payload);

        Assert.Contains($"\"kitHash\":\"{payload.KitHash}\"", kit, StringComparison.Ordinal);
        Assert.Contains($"\"benchHash\":\"{payload.BenchHash}\"", kit, StringComparison.Ordinal);
        Assert.Contains($"\"seed\":{payload.Seed}", kit, StringComparison.Ordinal);
        Assert.Contains($"\"runId\":\"{payload.RunId}\"", kit, StringComparison.Ordinal);
        Assert.DoesNotContain("/*{{KIT_PAYLOAD}}*/", kit, StringComparison.Ordinal);
    }

    /// <summary>
    /// La charge utile est inscrite dans un <c>&lt;script&gt;</c> : un <c>&lt;</c> littéral y
    /// fermerait la balise. L'encodeur strict le rend impossible, quoi qu'un mot du banc contienne.
    /// </summary>
    [Fact]
    public void Un_chevron_dans_un_indice_ne_peut_pas_fermer_la_balise_script()
    {
        var (bench, _) = Fixture();
        var piege = new GuessPlanItem("dev-000", "Top", "</script><script>alert(1)</script>");
        var payload = GuessKit.BuildPayload(
            bench, [piege], Manifest(bench), new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc));

        var kit = GuessKitPage.Build(RealPage, payload);

        Assert.DoesNotContain("</script><script>", kit, StringComparison.Ordinal);
        Assert.Contains("\\u003C", kit, StringComparison.Ordinal);
    }

    // ── Refus ───────────────────────────────────────────────────────────────

    [Fact]
    public void Une_page_sans_marqueur_d_ouverture_est_refusee()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => GuessKitPage.Build("<html><script>const a = 1;</script></html>", Payload()));

        Assert.Contains("transport:start", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Une_page_sans_marqueur_de_fermeture_est_refusee()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => GuessKitPage.Build("<script>// transport:start\nconst a = 1;</script>", Payload()));

        Assert.Contains("transport:end", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Deux ouvertures rendraient la région ambiguë, et la substitution emporterait silencieusement
    /// du code de rendu.
    /// </summary>
    [Fact]
    public void Un_marqueur_d_ouverture_en_double_est_refuse()
    {
        var page = "<script>// transport:start\na\n// transport:end\n// transport:start\nb</script>";

        var ex = Assert.Throws<InvalidOperationException>(() => GuessKitPage.Build(page, Payload()));

        Assert.Contains("plusieurs fois", ex.Message, StringComparison.Ordinal);
    }
}
