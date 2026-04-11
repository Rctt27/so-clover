using SoClover.Domain;

namespace SoClover.UseCases.Abstractions;

public interface IWordsPoolCache
{
    WordsPool? Get(GameId gameId);
    void Set(GameId gameId, WordsPool pool);
    void Remove(GameId gameId);
}
