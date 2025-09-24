using SoClover.Domain;

namespace SoClover.UseCases.Abstractions;

public interface IWordDictionary
{
    Task<IReadOnlyList<string>> TakeWords(GameId gameId, int count, CancellationToken ct = default);
}
