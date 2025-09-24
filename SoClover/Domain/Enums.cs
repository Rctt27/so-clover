namespace SoClover.Domain;

public enum Direction
{
    Top = 0,
    Right = 1,
    Bottom = 2,
    Left = 3
}

public enum Rotation
{
    None = 0,
    Right90 = 1,
    Right180 = 2,
    Right270 = 3
}

public enum GamePhase
{
    Lobby = 0,
    WritingClues = 1,
    Guessing = 2,
    Scoring = 3,
    Completed = 4
}

public enum BoardPosition
{
    TopLeft = 0,
    TopRight = 1,
    BottomRight = 2,
    BottomLeft = 3
}


