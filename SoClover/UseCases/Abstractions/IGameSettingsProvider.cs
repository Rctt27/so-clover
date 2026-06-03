namespace SoClover.UseCases.Abstractions;

public sealed class GameSettingsDto
{
    public int LobbyDurationSeconds { get; init; }
    public int CluesDurationSeconds { get; init; }
    public int GuessDurationSeconds { get; init; }
    public int ScoringDurationSeconds { get; init; }
    public int GuessingCooldownSeconds { get; init; }
    public int MaxAIPlayersPerGame { get; init; }
}

public interface IGameSettingsProvider
{
    Task<GameSettingsDto> GetAsync(CancellationToken ct = default);
}