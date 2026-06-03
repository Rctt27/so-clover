using Microsoft.Extensions.Configuration;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;

namespace SoClover.Tests;

public sealed class TestGameSettingsProvider : IGameSettingsProvider
{
    private readonly IConfiguration _config;

    public TestGameSettingsProvider()
    {
        var appSettingsPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SoClover", "appsettings.json"));
        _config = new ConfigurationBuilder()
            .AddJsonFile(appSettingsPath, optional: false)
            .Build();
    }

    public Task<GameSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var opts = _config.GetSection("GameDefaults").Get<GameDefaultsOptions>()
                   ?? new GameDefaultsOptions();
        int Clamp(int v) => Math.Clamp(v, 1, 1800);
        return Task.FromResult(new GameSettingsDto
        {
            LobbyDurationSeconds   = Clamp(opts.LobbyDuration  > 0 ? opts.LobbyDuration  : 600),
            CluesDurationSeconds   = Clamp(opts.CluesDuration  > 0 ? opts.CluesDuration  : 300),
            GuessDurationSeconds   = Clamp(opts.GuessDuration  > 0 ? opts.GuessDuration  : 300),
            ScoringDurationSeconds = Clamp(opts.ScoringDuration > 0 ? opts.ScoringDuration : 30),
            GuessingCooldownSeconds = Clamp(opts.GuessingCooldownDuration > 0 ? opts.GuessingCooldownDuration : 60),
            MaxAIPlayersPerGame = opts.MaxAIPlayersPerGame > 0 ? opts.MaxAIPlayersPerGame : 4
        });
    }
}
