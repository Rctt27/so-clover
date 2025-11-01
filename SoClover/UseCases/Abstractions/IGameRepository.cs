using SoClover.Domain;

namespace SoClover.UseCases.Abstractions;

public interface IGameRepository
{
    Task<Game?> Get(GameId id, CancellationToken ct = default);
    Task Save(Game game, CancellationToken ct = default);
    Task Delete(GameId id, CancellationToken ct = default);
    Task<IReadOnlyList<Game>> GetAll(CancellationToken ct = default);
}
