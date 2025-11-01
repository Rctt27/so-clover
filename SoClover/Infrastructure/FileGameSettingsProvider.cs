using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using SoClover.UseCases.Abstractions;

namespace SoClover.Infrastructure;

public sealed class FileGameSettingsProvider : IGameSettingsProvider
{
    private readonly string _settingsPath;

    private sealed class Raw
    {
        public string? Language { get; set; }
        public int lobbyDuration { get; set; }
        public int cluesDuration { get; set; }
        public int guessDuration { get; set; }
        public int scoringDuration { get; set; }
    }

    public FileGameSettingsProvider(IWebHostEnvironment env)
    {
        _settingsPath = Path.Combine(env.WebRootPath, "game_settings.json");
    }

    public async Task<GameSettingsDto> GetAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_settingsPath))
        {
            // Sensible defaults if file is missing
            return new GameSettingsDto { LobbyDurationSeconds = 600, CluesDurationSeconds = 300, GuessDurationSeconds = 300, ScoringDurationSeconds = 300 };
        }

        await using var stream = File.OpenRead(_settingsPath);
        var raw = await JsonSerializer.DeserializeAsync<Raw>(stream, cancellationToken: ct) ?? new Raw();
        int Clamp(int v, int min = 1, int max = 1800) => Math.Clamp(v, min, max);
        var lobby = raw.lobbyDuration > 0 ? raw.lobbyDuration : 600;
        var clues = raw.cluesDuration > 0 ? raw.cluesDuration : 300;
        var guess = raw.guessDuration > 0 ? raw.guessDuration : 300;
        var scoring = raw.scoringDuration > 0 ? raw.scoringDuration : 300;
        return new GameSettingsDto
        {
            LobbyDurationSeconds = Clamp(lobby),
            CluesDurationSeconds = Clamp(clues),
            GuessDurationSeconds = Clamp(guess),
            ScoringDurationSeconds = Clamp(scoring)
        };
    }
}