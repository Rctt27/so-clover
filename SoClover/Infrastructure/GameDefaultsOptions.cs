namespace SoClover.Infrastructure;

public sealed class GameDefaultsOptions
{
    public string Language { get; init; } = "Français_OFF";
    public int LobbyDuration { get; init; } = 600;
    public int CluesDuration { get; init; } = 300;
    public int GuessDuration { get; init; } = 300;
    public int ScoringDuration { get; init; } = 30;
}
