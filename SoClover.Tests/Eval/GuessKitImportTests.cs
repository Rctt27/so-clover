using SoClover.Eval.Bench;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Scoring;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Le kit est un transport, jamais un second protocole : ce qu'il rapporte doit produire un
/// fichier <b>indiscernable</b> de celui d'une séance servie, et refuser bruyamment tout ce qui ne
/// vient pas du kit envoyé. C'est le test central de la séance E — sans lui, « H2 a joué la même
/// séance que H1 » ne serait qu'une intention.
/// </summary>
public class GuessKitImportTests : IDisposable
{
    private static readonly DateTime Instant = new(2026, 8, 9, 14, 0, 0, DateTimeKind.Utc);

    private readonly List<string> _temp = [];

    public void Dispose()
    {
        foreach (var path in _temp.Where(File.Exists))
            File.Delete(path);
        GC.SuppressFinalize(this);
    }

    private string TempPath(string suffix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"kit-{Guid.NewGuid():N}{suffix}");
        _temp.Add(path);
        return path;
    }

    private static BenchContents Bench(int boardCount = 6) => HumanTestData.Bench(boardCount);

    private static IReadOnlyList<GuessPlanItem> Plan(BenchContents bench, string runId = "run-a") =>
        GuessingPlan.Build(
            bench, HumanTestData.Run(bench, runId, "gemma"), new HashSet<string>(), seed: 20260807001);

    private static GuessingManifest Manifest(
        BenchContents bench, long seed = 20260807001, string runId = "run-a") => new(
        "manifest", "eval/boards.dev.jsonl", bench.Manifest.BenchHash, seed, runId,
        "eval/runs/run-a.jsonl", null, [], 0, HumanFile.HarnessVersion, Instant);

    private static GuessKitPayload Payload(
        BenchContents bench, IReadOnlyList<GuessPlanItem> plan, GuessingManifest? manifest = null) =>
        GuessKit.BuildPayload(bench, plan, manifest ?? Manifest(bench), Instant);

    private static KitResultManifest Reported(
        GuessKitPayload payload,
        string? benchHash = null,
        long? seed = null,
        string? runId = null,
        string? kitHash = null,
        int? itemCount = null) => new(
        "kit-manifest",
        benchHash ?? payload.BenchHash,
        seed ?? payload.Seed,
        runId ?? payload.RunId,
        kitHash ?? payload.KitHash,
        payload.HarnessVersion,
        itemCount ?? payload.ItemCount,
        "e-20260809140000-ab12",
        Instant,
        Instant.AddMinutes(18),
        "Mozilla/5.0");

    private static KitResultGuess Answer(int index, IReadOnlyList<string> picked, long elapsedMs = 1000) =>
        new("kit-guess", index, picked, elapsedMs, Instant.AddSeconds(index * 25));

    /// <summary>La paire visée, telle que le devineur idéal la désignerait.</summary>
    private static IReadOnlyList<string> Cible(BenchContents bench, GuessPlanItem item) =>
        BenchBoardMapper.ReferenceWords(
            bench.Boards.Single(b => b.BoardId == item.BoardId),
            Enum.Parse<SoClover.Domain.Direction>(item.Direction));

    // ── L'invariant central ─────────────────────────────────────────────────

    /// <summary>
    /// Deux devineurs, deux transports, un seul format. On joue la MÊME suite de réponses à travers
    /// le serveur puis à travers le kit, et l'on exige des lignes identiques champ par champ —
    /// hormis l'identifiant de séance et l'horodatage, qui doivent justement différer.
    /// </summary>
    [Fact]
    public void Le_fichier_importe_est_identique_a_celui_d_une_seance_servie()
    {
        var bench = Bench();
        var plan = Plan(bench);
        var payload = Payload(bench, plan);

        var servedPath = TempPath(".jsonl");
        HumanFile.WriteGuessingManifest(servedPath, Manifest(bench));
        var session = new GuessingSession(
            bench, plan, HumanFile.ReadGuessing(servedPath), servedPath, "s-servie");

        var answers = new List<KitResultGuess>();
        for (var i = 0; i < plan.Count; i++)
        {
            var view = session.Next();

            // Le kit grave EXACTEMENT ce que le serveur sert — même indice, même ordre des seize
            // mots. C'est ici que « H2 voit ce que H1 a vu » cesse d'être une intention.
            Assert.Equal(view.Clue, payload.Items[i].Clue);
            Assert.Equal(view.PresentedWords, payload.Items[i].PresentedWords);

            // Un mélange de réponses justes, à moitié justes et fausses : R̄ doit survivre au
            // transport, pas seulement les mots.
            IReadOnlyList<string> picked = (i % 3) switch
            {
                0 => Cible(bench, plan[i]),
                1 => [Cible(bench, plan[i])[0], OtherWord(view.PresentedWords!, plan, bench, i)],
                _ => TwoWrongWords(view.PresentedWords!, plan, bench, i),
            };

            Assert.Equal(SessionStatus.Ok, session.SubmitGuess(picked, 1000).Status);
            answers.Add(Answer(i, picked));
        }

        var imported = GuessKitImport.BuildLines(
            bench, plan, payload, new KitResultContents(Reported(payload), answers));
        var served = HumanFile.ReadGuessing(servedPath).Guesses;

        Assert.Equal(served.Count, imported.Count);
        for (var i = 0; i < served.Count; i++)
        {
            // Comparaison sur la ligne JSONL sérialisée, et non sur le record : l'égalité de record
            // compare les listes par référence. C'est au passage la bonne exigence — ce sont les
            // OCTETS du corpus qui doivent coïncider, pas une notion d'égalité du langage.
            Assert.Equal(
                EvalJson.Serialize(served[i] with { SessionId = "x", GuessedAtUtc = Instant }),
                EvalJson.Serialize(imported[i] with { SessionId = "x", GuessedAtUtc = Instant }));
        }
    }

    private static string OtherWord(
        IReadOnlyList<string> presented, IReadOnlyList<GuessPlanItem> plan, BenchContents bench, int i)
    {
        var cible = Cible(bench, plan[i]);
        return presented.First(w => !cible.Contains(w, StringComparer.Ordinal));
    }

    private static IReadOnlyList<string> TwoWrongWords(
        IReadOnlyList<string> presented, IReadOnlyList<GuessPlanItem> plan, BenchContents bench, int i)
    {
        var cible = Cible(bench, plan[i]);
        return presented.Where(w => !cible.Contains(w, StringComparer.Ordinal)).Take(2).ToList();
    }

    /// <summary>
    /// Le <c>r</c> naît à l'import, sur la machine de l'opérateur, jamais dans la page. Les trois
    /// valeurs du barème doivent en sortir.
    /// </summary>
    [Fact]
    public void Le_score_est_calcule_a_l_import_sur_les_trois_valeurs_du_bareme()
    {
        var bench = Bench();
        var plan = Plan(bench);
        var payload = Payload(bench, plan);

        var presented0 = GuessingSession.PresentedWords(
            bench.Boards.Single(b => b.BoardId == plan[0].BoardId), bench.Manifest.BenchHash);
        var cible = Cible(bench, plan[0]);
        var faux = presented0.Where(w => !cible.Contains(w, StringComparer.Ordinal)).Take(2).ToList();

        double R(IReadOnlyList<string> picked) => GuessKitImport.BuildLines(
            bench, plan, payload,
            new KitResultContents(Reported(payload), [Answer(0, picked)]))[0].R;

        Assert.Equal(1.0, R(cible));
        Assert.Equal(0.5, R([cible[0], faux[0]]));
        Assert.Equal(0.0, R(faux));
    }

    /// <summary>Séance partielle : l'import consigne ce qui a été répondu, sans inventer le reste.</summary>
    [Fact]
    public void Une_seance_interrompue_s_importe_telle_quelle()
    {
        var bench = Bench();
        var plan = Plan(bench);
        var payload = Payload(bench, plan);

        var lines = GuessKitImport.BuildLines(
            bench, plan, payload,
            new KitResultContents(Reported(payload),
                [Answer(0, Cible(bench, plan[0])), Answer(1, Cible(bench, plan[1]))]));

        Assert.Equal(2, lines.Count);
        Assert.Equal([1, 2], lines.Select(l => l.ItemOrdinal));
    }

    // ── Concordance du montage ──────────────────────────────────────────────

    public static TheoryData<string> Divergences() => new() { "benchHash", "seed", "runId", "kitHash", "itemCount" };

    /// <summary>
    /// Un rapport qui ne vient pas du kit envoyé doit échouer, quelle que soit la case qui a bougé.
    /// Sans cette garde, un kit régénéré avec une autre graine produirait des lignes plausibles et
    /// fausses — et la séance E comparerait deux plans en croyant comparer deux personnes.
    /// </summary>
    [Theory]
    [MemberData(nameof(Divergences))]
    public void Un_montage_divergent_est_refuse(string champ)
    {
        var bench = Bench();
        var plan = Plan(bench);
        var payload = Payload(bench, plan);

        var reported = champ switch
        {
            "benchHash" => Reported(payload, benchHash: "ffffffffffff"),
            "seed" => Reported(payload, seed: 20260807002),
            "runId" => Reported(payload, runId: "run-b"),
            "kitHash" => Reported(payload, kitHash: "000000000000"),
            _ => Reported(payload, itemCount: payload.ItemCount + 1),
        };

        Assert.Throws<MismatchedBenchException>(() => GuessKitImport.BuildLines(
            bench, plan, payload,
            new KitResultContents(reported, [Answer(0, Cible(bench, plan[0]))])));
    }

    /// <summary>
    /// Le <c>kitHash</c> couvre les items eux-mêmes : deux plans qui partagent banc, graine et run
    /// mais divergent par les boards exclus ne peuvent pas se confondre.
    /// </summary>
    [Fact]
    public void Un_lot_amoindri_par_des_exclusions_ne_partage_pas_le_kitHash()
    {
        var bench = Bench();
        var complet = Payload(bench, Plan(bench));

        var reduit = GuessKit.BuildPayload(
            bench,
            GuessingPlan.Build(
                bench, HumanTestData.Run(bench, "run-a", "gemma"),
                new HashSet<string> { "dev-000" }, seed: 20260807001),
            Manifest(bench),
            Instant);

        Assert.NotEqual(complet.KitHash, reduit.KitHash);
    }

    // ── Rapports corrompus ou édités ────────────────────────────────────────

    [Fact]
    public void Un_itemIndex_hors_du_plan_est_refuse()
    {
        var bench = Bench();
        var plan = Plan(bench);
        var payload = Payload(bench, plan);

        var ex = Assert.Throws<HumanIntegrityException>(() => GuessKitImport.BuildLines(
            bench, plan, payload,
            new KitResultContents(Reported(payload), [Answer(plan.Count, ["a", "b"])])));

        Assert.Contains("hors du plan", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_itemIndex_rapporte_deux_fois_est_refuse()
    {
        var bench = Bench();
        var plan = Plan(bench);
        var payload = Payload(bench, plan);
        var cible = Cible(bench, plan[0]);

        var ex = Assert.Throws<HumanIntegrityException>(() => GuessKitImport.BuildLines(
            bench, plan, payload,
            new KitResultContents(Reported(payload), [Answer(0, cible), Answer(0, cible)])));

        Assert.Contains("deux fois", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// La garde décisive : les mots rapportés étaient-ils à l'écran ? Un rapport fabriqué ou
    /// produit sur un autre plan échoue ici même si son en-tête a été recopié.
    /// </summary>
    [Fact]
    public void Un_mot_jamais_presente_est_refuse()
    {
        var bench = Bench();
        var plan = Plan(bench);
        var payload = Payload(bench, plan);

        var ex = Assert.Throws<HumanIntegrityException>(() => GuessKitImport.BuildLines(
            bench, plan, payload,
            new KitResultContents(Reported(payload),
                [Answer(0, [Cible(bench, plan[0])[0], "mot-qui-n-existe-pas"])])));

        Assert.Contains("n'était pas présenté", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_nombre_de_mots_autre_que_deux_est_refuse()
    {
        var bench = Bench();
        var plan = Plan(bench);
        var payload = Payload(bench, plan);

        Assert.Throws<HumanIntegrityException>(() => GuessKitImport.BuildLines(
            bench, plan, payload,
            new KitResultContents(Reported(payload), [Answer(0, [Cible(bench, plan[0])[0]])])));
    }

    [Fact]
    public void Deux_fois_le_meme_mot_est_refuse()
    {
        var bench = Bench();
        var plan = Plan(bench);
        var payload = Payload(bench, plan);
        var mot = Cible(bench, plan[0])[0];

        Assert.Throws<HumanIntegrityException>(() => GuessKitImport.BuildLines(
            bench, plan, payload,
            new KitResultContents(Reported(payload), [Answer(0, [mot, mot])])));
    }

    // ── Écriture ────────────────────────────────────────────────────────────

    /// <summary>
    /// Le piège nommé au pré-enregistrement — un <c>--out</c> qui viserait la séance D — est ici
    /// rendu impossible, pas seulement déconseillé.
    /// </summary>
    [Fact]
    public void L_import_n_ecrase_jamais_un_corpus_existant()
    {
        var bench = Bench();
        var path = TempPath(".jsonl");
        File.WriteAllText(path, "déjà là\n");

        var ex = Assert.Throws<HumanIntegrityException>(
            () => GuessKitImport.Write(path, Manifest(bench), []));

        Assert.Contains("existe déjà", ex.Message, StringComparison.Ordinal);
        Assert.Equal("déjà là\n", File.ReadAllText(path).Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Le fichier écrit se relit par le lecteur ordinaire du harnais : c'est ce qui permet à
    /// <c>guess-report --guessing-b</c> de le consommer sans savoir qu'il vient d'un kit.
    /// </summary>
    [Fact]
    public void Le_fichier_ecrit_se_relit_comme_une_seance_ordinaire()
    {
        var bench = Bench();
        var plan = Plan(bench);
        var payload = Payload(bench, plan);
        var path = TempPath(".jsonl");

        var lines = GuessKitImport.BuildLines(
            bench, plan, payload,
            new KitResultContents(Reported(payload), [Answer(0, Cible(bench, plan[0]))]));
        GuessKitImport.Write(path, Manifest(bench), lines);

        var relu = HumanFile.ReadGuessing(path);

        Assert.Equal(bench.Manifest.BenchHash, relu.Manifest.BenchHash);
        Assert.Single(relu.Guesses);
        Assert.Equal("e-20260809140000-ab12", relu.Guesses[0].SessionId);
        Assert.Equal(1.0, relu.Guesses[0].R);
    }

    // ── Lecture du rapport ──────────────────────────────────────────────────

    [Fact]
    public void Un_rapport_dont_l_entete_n_est_pas_un_kit_est_refuse()
    {
        var path = TempPath(".jsonl");
        File.WriteAllText(path, "{\"kind\":\"manifest\"}\n");

        var ex = Assert.Throws<HumanIntegrityException>(() => KitResultFile.Read(path));

        Assert.Contains("Enregistrer mes réponses", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// L'étiquette est un confort de classement quand plusieurs personnes jouent le même kit. Elle
    /// est facultative des deux côtés : absente du fichier, elle vaut <c>null</c> sans faire échouer
    /// la lecture — et elle n'entre dans aucun calcul.
    /// </summary>
    [Fact]
    public void L_etiquette_du_devineur_est_facultative()
    {
        var sans = TempPath(".jsonl");
        var avec = TempPath(".jsonl");
        const string Entete =
            "{\"kind\":\"kit-manifest\",\"benchHash\":\"aaaaaaaaaaaa\",\"seed\":1,\"runId\":\"r\"," +
            "\"kitHash\":\"k\",\"harnessVersion\":1,\"itemCount\":1,\"sessionId\":\"e-1-ab\"," +
            "\"startedAtUtc\":\"2026-08-09T14:00:00Z\",\"downloadedAtUtc\":\"2026-08-09T14:18:00Z\"," +
            "\"userAgent\":\"UA\"";

        File.WriteAllText(sans, Entete + "}\n");
        File.WriteAllText(avec, Entete + ",\"label\":\"marie\"}\n");

        Assert.Null(KitResultFile.Read(sans).Manifest.Label);
        Assert.Equal("marie", KitResultFile.Read(avec).Manifest.Label);
    }

    /// <summary>
    /// Deux devineurs qui joueraient le même kit produisent le même <c>kitHash</c> — c'est
    /// l'empreinte du MONTAGE, pas de la personne. Ce qui les distingue est le <c>sessionId</c>, et
    /// c'est de lui seul que doit dépendre l'unicité des fichiers reçus.
    /// </summary>
    [Fact]
    public void Deux_devineurs_du_meme_kit_partagent_le_kitHash_et_pas_la_session()
    {
        var bench = Bench();
        var plan = Plan(bench);
        var payload = Payload(bench, plan);

        var marie = Reported(payload) with { SessionId = "e-20260809140000-ab12", Label = "marie" };
        var paul = Reported(payload) with { SessionId = "e-20260809181500-3f7c", Label = "paul" };

        Assert.Equal(marie.KitHash, paul.KitHash);
        Assert.NotEqual(marie.SessionId, paul.SessionId);

        var lignesMarie = GuessKitImport.BuildLines(
            bench, plan, payload, new KitResultContents(marie, [Answer(0, Cible(bench, plan[0]))]));
        var lignesPaul = GuessKitImport.BuildLines(
            bench, plan, payload, new KitResultContents(paul, [Answer(0, Cible(bench, plan[0]))]));

        // Le sessionId part dans chaque ligne : c'est ce que RequireDistinctSessions relit pour
        // refuser « la même séance, pas deux devineurs ».
        Assert.Equal("e-20260809140000-ab12", lignesMarie[0].SessionId);
        Assert.Equal("e-20260809181500-3f7c", lignesPaul[0].SessionId);
    }

    [Fact]
    public void Le_rapport_produit_par_le_kit_se_relit_en_entier()
    {
        var path = TempPath(".jsonl");
        File.WriteAllText(path,
            "{\"kind\":\"kit-manifest\",\"benchHash\":\"aaaaaaaaaaaa\",\"seed\":20260807001," +
            "\"runId\":\"run-a\",\"kitHash\":\"0123456789ab\",\"harnessVersion\":1,\"itemCount\":2," +
            "\"sessionId\":\"e-20260809140000-ab12\",\"startedAtUtc\":\"2026-08-09T14:00:00.000Z\"," +
            "\"downloadedAtUtc\":\"2026-08-09T14:18:00.000Z\",\"userAgent\":\"Mozilla/5.0\"}\n" +
            "{\"kind\":\"kit-guess\",\"itemIndex\":0,\"picked\":[\"un\",\"deux\"],\"elapsedMs\":1234," +
            "\"guessedAtUtc\":\"2026-08-09T14:00:25.000Z\"}\n");

        var contents = KitResultFile.Read(path);

        Assert.Equal("0123456789ab", contents.Manifest.KitHash);
        Assert.Equal(20260807001, contents.Manifest.Seed);
        Assert.Single(contents.Guesses);
        Assert.Equal(["un", "deux"], contents.Guesses[0].Picked);
        Assert.Equal(1234, contents.Guesses[0].ElapsedMs);
    }
}
