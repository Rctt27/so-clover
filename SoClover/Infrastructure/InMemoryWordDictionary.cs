using System.Collections.Concurrent;
using SoClover.Domain;
using SoClover.UseCases.Abstractions;

namespace SoClover.Infrastructure;

public sealed class InMemoryWordDictionary : IWordDictionary
{
    private static readonly string[] SeedWords =
    [
        "CAT","DOG","MOUSE","BIRD","SUN","MOON","STAR","SKY",
        "RED","BLUE","GREEN","YELLOW","WATER","FIRE","EARTH","WIND",
        "TREE","ROCK","RIVER","MOUNTAIN","CAR","TRAIN","PLANE","BOAT"
    ];

    private readonly ConcurrentDictionary<GameId, Queue<string>> _perGame = new();

    public Task<IReadOnlyList<string>> TakeWords(GameId gameId, int count, CancellationToken ct = default)
    {
        var queue = _perGame.GetOrAdd(gameId, _ => new Queue<string>(Shuffle(SeedWords)));
        // Ensure there are enough words; if not, recycle the seed words with a fresh shuffle
        while (queue.Count < count)
        {
            foreach (var w in Shuffle(SeedWords))
                queue.Enqueue(w);
        }
        var list = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(queue.Dequeue());
        }
        return Task.FromResult<IReadOnlyList<string>>(list);
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
