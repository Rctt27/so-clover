using SoClover.Domain;
using SoClover.Domain.Validation;
using Xunit;

namespace SoClover.Tests.Helpers;

/// <summary>
/// Verrouille les deux invariants qui rendent <see cref="DeterministicWordDictionary"/> insensible au
/// tirage de mots — c'est-à-dire les deux hypothèses implicites que le vrai dictionnaire ne garantit pas
/// et qui rendaient la suite intermittente (cf. commentaire de tête de la classe).
/// </summary>
public class DeterministicWordDictionaryTests
{
    private const string AnyLanguage = "Français_OFF";

    // Marge au-dessus de MinBoardWordLength (2) de SubstringClueValidator : on exige davantage que le
    // seuil produit pour rester insensible à un futur relèvement de celui-ci.
    private const int SafeWordLength = 3;

    /// <summary>
    /// Indices littéraux posés par les suites câblées sur ce dictionnaire. Un test qui introduit un
    /// nouvel indice codé en dur doit l'ajouter ici : c'est ce qui prouve qu'aucun mot de carte ne peut
    /// le faire rejeter par accident.
    /// </summary>
    public static readonly string[] ClueLiteralsUsedByTests =
    {
        "admin-top", "admin-right", "admin-bottom", "admin-left",
        "bob-top", "bob-right", "bob-bottom", "bob-left",
        "carol-top", "carol-right", "carol-bottom", "carol-left",
        "ai-top", "ai-right", "ai-bottom", "ai-left",
        "CL Admin Top", "CL Admin Right", "CL Admin Bottom", "CL Admin Left",
        "CL Bob Top", "CL Bob Right", "CL Bob Bottom", "CL Bob Left",
        "zzqxkj0000", "zzqxkj0001", "zzqxkj0002", "zzqxkj0003",
    };

    private static readonly IClueValidator[] Validators =
    {
        new FrenchOffClueValidator(),
        new EnglishOffClueValidator(),
        new PortugueseOffClueValidator(),
    };

    [Fact]
    public async Task Every_word_is_long_enough_to_be_seen_by_the_clue_validators()
    {
        // Historiquement : « Nu », « Os », « Or » (FR) passaient sous le seuil de visibilité et étaient
        // IGNORÉS du validateur, si bien qu'un test posant un mot du board comme indice en attendant un
        // rejet le voyait accepté. Le seuil produit a été abaissé depuis ; on garde la marge ici.
        var words = await new DeterministicWordDictionary().GetAllWordsAsync(AnyLanguage);

        Assert.NotEmpty(words);
        Assert.All(words, w => Assert.True(
            TextNormalizer.Normalize(w).Length >= SafeWordLength,
            $"'{w}' est trop court pour être vu par le validateur."));
    }

    [Fact]
    public async Task A_board_word_used_as_a_clue_is_always_rejected()
    {
        // Corollaire opérationnel de l'invariant précédent, exprimé sur l'API réellement utilisée par
        // les tests IA (« je prends un mot du board comme indice, il doit être rejeté »).
        var words = await new DeterministicWordDictionary().GetAllWordsAsync(AnyLanguage);

        foreach (var validator in Validators)
        foreach (var card in CardsOver(words))
        {
            var board = new CloverBoard();
            board.Place(BoardPosition.TopLeft, new OrientedCard(card));

            foreach (var word in new[] { card.TopWord, card.RightWord, card.BottomWord, card.LeftWord })
            {
                var result = validator.Validate(word, Direction.Top, board);
                Assert.False(result.IsValid,
                    $"[{validator.Language}] '{word}' est un mot du board mais n'a pas été rejeté.");
            }
        }
    }

    [Fact]
    public async Task No_word_can_reject_a_clue_literal_used_by_the_tests()
    {
        // Le vrai dictionnaire FR contient « Botte » : sa racine R2 « bott » est une sous-chaîne de
        // « admin-bottom », donc tirer cette carte faisait rejeter l'indice et laissait le board
        // incomplet (SubmitBoard → « Cannot submit an incomplete board »).
        var words = await new DeterministicWordDictionary().GetAllWordsAsync(AnyLanguage);

        foreach (var validator in Validators)
        foreach (var board in FullBoardsOver(words))
        foreach (var clue in ClueLiteralsUsedByTests)
        {
            var result = validator.Validate(clue, Direction.Top, board);
            Assert.True(result.IsValid,
                $"[{validator.Language}] '{clue}' rejeté par {string.Join(", ", result.Errors.Select(e => $"{e.Rule}:{e.CardWord}"))}.");
        }
    }

    [Fact]
    public async Task Every_word_carries_a_digit_so_no_letters_only_clue_can_contain_it()
    {
        // Invariant structurel : il rend la première branche de R1 (l'indice contient le mot) et R2
        // inatteignables pour tout indice fait de lettres, indépendamment de la liste ci-dessus.
        var words = await new DeterministicWordDictionary().GetAllWordsAsync(AnyLanguage);

        Assert.All(words, w => Assert.Contains(w, char.IsDigit));
    }

    [Fact]
    public async Task Provides_enough_distinct_words_for_a_full_game()
    {
        // 4 joueurs × 4 cartes × 4 mots = 64 mots tirés sans remise, plus les cartes recréées en
        // phase de devinette.
        var words = await new DeterministicWordDictionary().GetAllWordsAsync(AnyLanguage);

        Assert.True(words.Count >= 256, $"Seulement {words.Count} mots disponibles.");
        Assert.Equal(words.Count, words.Distinct().Count());
    }

    [Fact]
    public async Task Serves_every_language_with_the_same_vocabulary()
    {
        var dict = new DeterministicWordDictionary();

        var fr = await dict.GetAllWordsAsync("Français_OFF");
        var en = await dict.GetAllWordsAsync("English_(from_FR_OFF)");

        Assert.Equal(fr, en);
    }

    [Fact]
    public async Task GetRandomWordsAsync_returns_the_requested_count_of_distinct_words()
    {
        // Utilisé par GameCodeGenerator : deux appels doivent pouvoir produire deux codes différents,
        // sinon le générateur boucle sur un code déjà pris.
        var dict = new DeterministicWordDictionary();

        var first = await dict.GetRandomWordsAsync(AnyLanguage, 4);
        var second = await dict.GetRandomWordsAsync(AnyLanguage, 4);

        Assert.Equal(4, first.Count);
        Assert.Equal(4, first.Distinct().Count());
        Assert.NotEqual(first, second);
    }

    private static IEnumerable<Card> CardsOver(IReadOnlyList<string> words)
    {
        for (var i = 0; i + 4 <= words.Count; i += 4)
            yield return new Card(CardId.New(), words[i], words[i + 1], words[i + 2], words[i + 3]);
    }

    private static IEnumerable<CloverBoard> FullBoardsOver(IReadOnlyList<string> words)
    {
        var cards = CardsOver(words).ToList();
        for (var i = 0; i + 4 <= cards.Count; i += 4)
        {
            var board = new CloverBoard();
            board.Place(BoardPosition.TopLeft, new OrientedCard(cards[i]));
            board.Place(BoardPosition.TopRight, new OrientedCard(cards[i + 1]));
            board.Place(BoardPosition.BottomRight, new OrientedCard(cards[i + 2]));
            board.Place(BoardPosition.BottomLeft, new OrientedCard(cards[i + 3]));
            yield return board;
        }
    }
}
