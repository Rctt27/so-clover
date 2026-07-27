using SoClover.Domain;
using SoClover.Eval.Io;

namespace SoClover.Eval.Bench;

/// <summary>
/// Génération seedée d'un banc reproduisant la distribution réelle du jeu : 16 mots tirés sans
/// remise dans le dictionnaire complet, découpés en 4 cartes de 4, paires de référence dérivées
/// par <see cref="BoardGeometry"/>.
/// <para>
/// Aucune contrainte de rôle entre mots de référence et distracteurs — le tirage étant uniforme,
/// la répartition l'est aussi. L'objectif de couverture uniforme du dictionnaire de l'ancien plan
/// <c>Specs/AI_Clue_Dataset</c> est précisément ce que le PRD abandonne.
/// </para>
/// </summary>
public static class BenchGenerator
{
    public const int GeneratorVersion = 1;
    public const int BenchHashHexLength = 12;

    private const int WordsPerBoard = 16;
    private const int WordsPerCard = 4;

    public static BenchContents Generate(
        string benchId,
        long seed,
        int boardCount,
        IReadOnlyList<string> dictionary,
        string language,
        string dictionaryFile,
        string dictionaryHash,
        DateTime createdAtUtc)
    {
        if (boardCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(boardCount), boardCount, "boardCount doit être > 0.");
        if (dictionary.Count < WordsPerBoard)
            throw new ArgumentException(
                $"Le dictionnaire doit contenir au moins {WordsPerBoard} mots (il en a {dictionary.Count}).",
                nameof(dictionary));

        // Un seul générateur par fichier de banc, consommé linéairement.
        var rng = new Xoshiro256SS(seed);
        var boards = new List<BenchBoard>(boardCount);

        for (var i = 0; i < boardCount; i++)
        {
            // Fisher-Yates COMPLET sur tout le dictionnaire (~880 mots) alors que seuls les 16
            // premiers servent. C'est VOLONTAIRE — NE PAS "optimiser" en shuffle partiel ou en
            // reservoir sampling : la consommation du PRNG (le nombre d'appels à rng.Next et leur
            // ordre) fait partie du contrat de gel du banc. Un shuffle partiel consommerait un
            // nombre d'octets aléatoires différent et changerait TOUS les boards à partir de là,
            // invalidant silencieusement les deux bancs déjà gelés et committés.
            var pool = dictionary.ToList();
            rng.Shuffle(pool);

            var cards = new List<IReadOnlyList<string>>(4);
            for (var c = 0; c < 4; c++)
                cards.Add(pool.GetRange(c * WordsPerCard, WordsPerCard).AsReadOnly());

            var readOnlyCards = cards.AsReadOnly();
            var directions = BoardGeometry.AllDirections
                .Select(d => new BenchDirection(
                    d.ToString(),
                    BenchBoardMapper.DeriveReferenceWords(readOnlyCards, d)))
                .ToList()
                .AsReadOnly();

            boards.Add(new BenchBoard(
                Kind: "board",
                BoardId: $"{benchId}-{i + 1:D3}",
                Cards: readOnlyCards,
                Directions: directions,
                Strata: new BenchStrata(DrawDifficulty: null)));
        }

        var readOnlyBoards = boards.AsReadOnly();
        // Passe par BenchFile.ComputeBenchHash — point d'entrée unique partagé avec Read, qui
        // revalide ce même hash à la lecture. Ne pas dupliquer ce calcul ici.
        var benchHash = BenchFile.ComputeBenchHash(readOnlyBoards);

        var manifest = new BenchManifest(
            Kind: "manifest",
            BenchId: benchId,
            Seed: seed,
            BoardCount: boardCount,
            Language: language,
            DictionaryFile: dictionaryFile,
            DictionaryHash: dictionaryHash,
            PrngAlgorithm: Xoshiro256SS.AlgorithmName,
            GeneratorVersion: GeneratorVersion,
            CreatedAtUtc: createdAtUtc,
            BenchHash: benchHash);

        return new BenchContents(manifest, readOnlyBoards);
    }
}
