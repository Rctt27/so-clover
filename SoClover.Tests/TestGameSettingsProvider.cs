using System.Text.Json;
using SoClover.UseCases.Abstractions;

namespace SoClover.Tests;

public sealed class TestGameSettingsProvider : IGameSettingsProvider
{
    private readonly string _filePath;

    public TestGameSettingsProvider(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<GameSettingsDto> GetAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
        {
            return new GameSettingsDto { LobbyDurationSeconds = 600, CluesDurationSeconds = 300, GuessDurationSeconds = 300, ScoringDurationSeconds = 300 };
        }

        await using var stream = File.OpenRead(_filePath);
        var raw = await JsonSerializer.DeserializeAsync<RawSettings>(stream, cancellationToken: ct) ?? new RawSettings();
        int Clamp(int v, int min = 1, int max = 1800) => Math.Clamp(v, min, max);
        return new GameSettingsDto
        {
            LobbyDurationSeconds = Clamp(raw.lobbyDuration > 0 ? raw.lobbyDuration : 600),
            CluesDurationSeconds = Clamp(raw.cluesDuration > 0 ? raw.cluesDuration : 300),
            GuessDurationSeconds = Clamp(raw.guessDuration > 0 ? raw.guessDuration : 300),
            ScoringDurationSeconds = Clamp(raw.scoringDuration > 0 ? raw.scoringDuration : 300)
        };
    }

    private sealed class RawSettings
    {
        public int lobbyDuration { get; set; }
        public int cluesDuration { get; set; }
        public int guessDuration { get; set; }
        public int scoringDuration { get; set; }
    }
}
