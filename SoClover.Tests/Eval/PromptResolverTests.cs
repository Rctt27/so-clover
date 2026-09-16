using SoClover.Domain;
using SoClover.Eval.Calibration;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Prompts;
using SoClover.Infrastructure.AI.Prompts;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public sealed class PromptResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "soclover-prompts-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static string PackagedClue() => File.ReadAllText(SoCloverPrompt.DecoderFrClue.PackagedPath);

    private static PromptSelection Langfuse(string? label = "production", int? version = null) =>
        new(PromptSource.Langfuse, version is null ? label : null, version);

    [Fact]
    public async Task La_source_fichier_rend_le_prompt_embarque_et_son_sha()
    {
        var resolver = new PromptResolver(null, _root);

        var resolved = await resolver.ResolveAsync(
            SoCloverPrompt.DecoderFrClue, new PromptSelection(PromptSource.File, null, null), CancellationToken.None);

        Assert.Equal(SoCloverPrompt.DecoderFrClue.PackagedPath, resolved.Path);
        Assert.Equal("file", resolved.Provenance.Source);
        Assert.Equal(PromptContent.Sha256(PackagedClue()), resolved.Provenance.ContentSha256);
        Assert.Equal(PromptContent.DeclaredVersion(PackagedClue()), resolved.DeclaredVersion);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task La_source_Langfuse_materialise_sous_un_chemin_qui_garde_fr_et_le_nom_de_fichier()
    {
        var handler = FakeLangfusePrompts.Serve("decoder-fr-clue", (3, PackagedClue(), ["v4", "production"]));
        var resolver = new PromptResolver(LangfuseStubHandler.Client(handler), _root);

        var resolved = await resolver.ResolveAsync(SoCloverPrompt.DecoderFrClue, Langfuse(), CancellationToken.None);

        var sha = PromptContent.Sha256(PackagedClue());
        Assert.Equal(Path.Combine(_root, sha[..12], "fr", "decode-clue.md"), resolved.Path);
        Assert.Equal(PromptContent.Normalize(PackagedClue()), File.ReadAllText(resolved.Path));
        Assert.Equal(new PromptProvenance("langfuse", "decoder-fr-clue", 3, "production", sha), resolved.Provenance);
    }

    /// <summary>
    /// Le cœur de la spec §6.4 : un décodeur servi par Langfuse, de contenu identique au fichier,
    /// est LE MÊME décodeur pour le registre. Sans cette égalité, toutes les lignes existantes
    /// cesseraient d'être comparables au premier run tiré de Langfuse.
    /// </summary>
    [Fact]
    public async Task Un_contenu_identique_donne_la_meme_empreinte_de_decodeur_que_le_fichier()
    {
        var handler = FakeLangfusePrompts.Serve("decoder-fr-clue", (3, PackagedClue().Replace("\n", "\r\n"), ["production"]));
        var fromLangfuse = await new PromptResolver(LangfuseStubHandler.Client(handler), _root)
            .ResolveAsync(SoCloverPrompt.DecoderFrClue, Langfuse(), CancellationToken.None);
        var fromFile = await new PromptResolver(null, _root)
            .ResolveAsync(SoCloverPrompt.DecoderFrClue, new PromptSelection(PromptSource.File, null, null), CancellationToken.None);

        Assert.Equal(
            DecoderFingerprint.Compute("qwen/qwen3-8b", fromFile.Path, fromFile.DeclaredVersion, 0.3, 1.0, 512),
            DecoderFingerprint.Compute("qwen/qwen3-8b", fromLangfuse.Path, fromLangfuse.DeclaredVersion, 0.3, 1.0, 512));
    }

    [Fact]
    public async Task Une_version_explicite_est_resolue_sans_label_dans_la_provenance()
    {
        const string v5 = "---\nversion: 5\n---\n# SYSTEM\nautre\n";
        var handler = FakeLangfusePrompts.Serve("decoder-fr-clue", (1, PackagedClue(), ["production"]), (2, v5, ["candidat"]));

        var resolved = await new PromptResolver(LangfuseStubHandler.Client(handler), _root)
            .ResolveAsync(SoCloverPrompt.DecoderFrClue, Langfuse(version: 2), CancellationToken.None);

        Assert.Equal(5, resolved.DeclaredVersion);
        Assert.Equal(2, resolved.Provenance.LangfuseVersion);
        Assert.Null(resolved.Provenance.LangfuseLabel);
    }

    [Fact]
    public async Task Un_historique_en_conflit_est_refuse_avant_toute_materialisation()
    {
        const string edited = "---\nversion: 4\n---\n# SYSTEM\nédité sans bump\n";
        var handler = FakeLangfusePrompts.Serve("decoder-fr-clue",
            (1, "---\nversion: 4\n---\n# SYSTEM\norigine\n", ["v4"]), (2, edited, ["production"]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new PromptResolver(LangfuseStubHandler.Client(handler), _root)
            .ResolveAsync(SoCloverPrompt.DecoderFrClue, Langfuse(), CancellationToken.None));

        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task Sans_client_la_source_Langfuse_est_refusee_en_nommant_le_repli()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new PromptResolver(null, _root)
            .ResolveAsync(SoCloverPrompt.DecoderFrClue, Langfuse(), CancellationToken.None));

        Assert.Contains("--prompt-source file", ex.Message);
    }

    [Fact]
    public async Task Un_label_introuvable_est_refuse_en_nommant_langfuse_sync()
    {
        var handler = FakeLangfusePrompts.Serve("decoder-fr-clue", (1, PackagedClue(), ["v4"]));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new PromptResolver(LangfuseStubHandler.Client(handler), _root)
            .ResolveAsync(SoCloverPrompt.DecoderFrClue, Langfuse("production"), CancellationToken.None));

        Assert.Contains("langfuse-sync", ex.Message);
    }

    [Fact]
    public async Task Un_prompt_hors_Langfuse_refuse_la_source_Langfuse()
    {
        var handler = FakeLangfusePrompts.Serve("decoder-fr-clue");

        await Assert.ThrowsAsync<InvalidOperationException>(() => new PromptResolver(LangfuseStubHandler.Client(handler), _root)
            .ResolveAsync(SoCloverPrompt.GeneratorFrPerDirectionReasoning, Langfuse(), CancellationToken.None));
    }

    [Fact]
    public async Task Un_fichier_materialise_altere_est_refuse()
    {
        var handler = FakeLangfusePrompts.Serve("decoder-fr-clue", (1, PackagedClue(), ["production"]));
        var resolver = new PromptResolver(LangfuseStubHandler.Client(handler), _root);
        var first = await resolver.ResolveAsync(SoCloverPrompt.DecoderFrClue, Langfuse(), CancellationToken.None);
        File.AppendAllText(first.Path, "\najout manuel");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(SoCloverPrompt.DecoderFrClue, Langfuse(), CancellationToken.None));
    }

    [Fact]
    public void Describe_nomme_source_versions_et_sha_court()
    {
        var sha = new string('a', 64);

        Assert.Equal("v4 (langfuse decoder-fr-clue #3, label production, sha aaaaaaaa)",
            new ResolvedPrompt("x", 4, new PromptProvenance("langfuse", "decoder-fr-clue", 3, "production", sha)).Describe());
        Assert.Equal("v4 (langfuse decoder-fr-clue #3, sha aaaaaaaa)",
            new ResolvedPrompt("x", 4, new PromptProvenance("langfuse", "decoder-fr-clue", 3, null, sha)).Describe());
        Assert.Equal("v4 (fichier, sha aaaaaaaa)",
            new ResolvedPrompt("x", 4, new PromptProvenance("file", null, null, null, sha)).Describe());
    }

    [Fact]
    public void Le_constructeur_public_a_chemins_rend_le_meme_prompt_que_le_constructeur_par_defaut()
    {
        var context = new BoardCluesPromptContext(
            "Français_OFF",
            [
                new BoardCardSnapshot(BoardPosition.TopLeft, "Lune", "Route", "Plage", "Ciel"),
                new BoardCardSnapshot(BoardPosition.TopRight, "Vague", "Rocher", "Sable", "Île"),
                new BoardCardSnapshot(BoardPosition.BottomRight, "Oiseau", "Forêt", "Montagne", "Vent"),
                new BoardCardSnapshot(BoardPosition.BottomLeft, "Rivière", "Pont", "Ville", "Village"),
            ],
            [Direction.Top],
            new Dictionary<Direction, IReadOnlyList<RejectedAttempt>>());

        var byDefault = new FrenchAiCluePromptProvider().BuildSingleDirectionCluePrompt(context);
        var byPaths = new FrenchAiCluePromptProvider(
                new FilePromptLoader(),
                Path.Combine(AppContext.BaseDirectory, "Infrastructure", "AI", "Prompts", "fr", "board-clues.md"),
                SoCloverPrompt.GeneratorFrPerDirection.PackagedPath,
                SoCloverPrompt.GeneratorFrPerDirectionReasoning.PackagedPath)
            .BuildSingleDirectionCluePrompt(context);

        Assert.Equal(byDefault, byPaths);
    }
}
