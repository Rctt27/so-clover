using Microsoft.Extensions.Options;
using SoClover.UseCases.Abstractions;

namespace SoClover.Infrastructure;

public sealed class ConfigurationGameSettingsProvider(IOptions<GameDefaultsOptions> options)
    : IGameSettingsProvider
{
    public Task<GameSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var o = options.Value;
        int Clamp(int v) => Math.Clamp(v, 1, 1800);
        return Task.FromResult(new GameSettingsDto
        {
            LobbyDurationSeconds   = Clamp(o.LobbyDuration  > 0 ? o.LobbyDuration  : 600),
            CluesDurationSeconds   = Clamp(o.CluesDuration  > 0 ? o.CluesDuration  : 300),
            GuessDurationSeconds   = Clamp(o.GuessDuration  > 0 ? o.GuessDuration  : 300),
            ScoringDurationSeconds = Clamp(o.ScoringDuration > 0 ? o.ScoringDuration : 30),
            MaxAIPlayersPerGame = o.MaxAIPlayersPerGame > 0 ? o.MaxAIPlayersPerGame : 4
        });
    }
}
