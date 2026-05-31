using System.Threading;
using System.Threading.Tasks;

namespace SoClover.UseCases.GameLogics;

public interface IGameCodeGenerator
{
    Task<string> GenerateAsync(CancellationToken ct = default);
}
