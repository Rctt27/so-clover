using SoClover.Domain;

namespace SoClover.Infrastructure;

public sealed class InMemoryWordDictionary : IWordDictionary
{
    private static readonly Dictionary<string, string[]> SeedWordsByLanguage = new()
    {
        ["Français"] = 
        [
            "CHAT", "CHIEN", "SOURIS", "OISEAU", "SOLEIL", "LUNE", "ÉTOILE", "CIEL",
            "ROUGE", "BLEU", "VERT", "JAUNE", "EAU", "FEU", "TERRE", "VENT",
            "ARBRE", "ROCHE", "RIVIÈRE", "MONTAGNE", "VOITURE", "TRAIN", "AVION", "BATEAU"
        ],
        ["English"] =
        [
            "CAT", "DOG", "MOUSE", "BIRD", "SUN", "MOON", "STAR", "SKY",
            "RED", "BLUE", "GREEN", "YELLOW", "WATER", "FIRE", "EARTH", "WIND",
            "TREE", "ROCK", "RIVER", "MOUNTAIN", "CAR", "TRAIN", "PLANE", "BOAT"
        ]
    };

    public Task<IReadOnlyList<string>> GetRandomWordsAsync(string language, int count, CancellationToken ct = default)
    {
        if (!SeedWordsByLanguage.TryGetValue(language, out var seedWords))
        {
            // Default to French if language not found
            seedWords = SeedWordsByLanguage["Français"];
        }

        var shuffled = Shuffle(seedWords).ToArray();
        var result = new List<string>(count);

        for (int i = 0; i < count; i++)
        {
            result.Add(shuffled[i % shuffled.Length]);
        }

        return Task.FromResult<IReadOnlyList<string>>(result);
    }

    private static IEnumerable<string> Shuffle(IEnumerable<string> input)
    {
        var rng = Random.Shared;
        var arr = input.ToArray();
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return arr;
    }
}
