using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Boards;
using SoClover.UseCases.Errors;
using SoClover.UseCases.Games;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddSingleton<IGameRepository, InMemoryGameRepository>();
builder.Services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
builder.Services.AddSingleton<IWordDictionary>(sp => 
    new FileWordDictionary(Path.Combine(builder.Environment.WebRootPath, "dictionaries")));

// Domain services
builder.Services.AddTransient<CardFactory>();

// Use cases
builder.Services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
builder.Services.AddTransient<IDeleteGameUseCase, DeleteGame.Handler>();
builder.Services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
builder.Services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
builder.Services.AddTransient<ISetClueUseCase, SetClue.Handler>();
builder.Services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
builder.Services.AddTransient<IGuessUseCase, Guess.Handler>();
builder.Services.AddTransient<IPlaceCardUseCase, PlaceCard.Handler>();
builder.Services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
builder.Services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();
builder.Services.AddTransient<IPlaceGuessingCardUseCase, PlaceGuessingCard.Handler>();
builder.Services.AddTransient<IRotateBoardCardUseCase, RotateBoardCard.Handler>();
builder.Services.AddTransient<IRotateOutsideCardUseCase, RotateOutsideCard.Handler>();
builder.Services.AddTransient<IValidateGuessingBoardUseCase, ValidateGuessingBoard.Handler>();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.UseStaticFiles();

// API Endpoints
app.MapPost("/api/games", async (CreateGameRequest? request, ICreateGameUseCase useCase, CancellationToken ct) =>
{
    var response = await useCase.Handle(new CreateGame.Request(request?.Language), ct);
    return Results.Ok(new { gameId = response.GameId.Value });
})
.WithName("CreateGame");

app.MapDelete("/api/games/{gameId:guid}", async (Guid gameId, IDeleteGameUseCase useCase, CancellationToken ct) =>
{
    var request = new DeleteGame.Request(new GameId(gameId));
    var response = await useCase.Handle(request, ct);

    if (!response.Success)
    {
        return Results.NotFound(new { message = "Game not found" });
    }

    return Results.Ok(new { message = "Game deleted successfully" });
})
.WithName("DeleteGame");

app.MapPost("/api/games/{gameId:guid}/join", async (Guid gameId, JoinGameRequest? request, IJoinGameUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerName))
    {
        return Results.BadRequest(new { message = "Player name is required" });
    }

    try
    {
        var response = await useCase.Handle(new JoinGame.Request(new GameId(gameId), request.PlayerName), ct);
        return Results.Ok(new { playerId = response.PlayerId.Value });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
})
.WithName("JoinGame");

app.MapGet("/api/games/{gameId:guid}/state", async (Guid gameId, string? playerId, bool includeSecrets, IGetGameStateUseCase useCase, CancellationToken ct) =>
{
    try
    {
        var response = await useCase.Handle(new GetGameState.Request(new GameId(gameId), includeSecrets), ct);
        var result = new
        {
            gameId = response.GameId.Value,
            phase = response.Phase.ToString(),
            guessingState = response.GuessingState == null ? null : new
            {
                currentBoardOwnerId = response.GuessingState.CurrentBoardOwnerId?.Value,
                currentBoardOwnerName = response.GuessingState.CurrentBoardOwnerName,
                outsideCards = response.GuessingState.OutsideCards.Select(c => new
                {
                    cardId = c.CardId,
                    topWord = c.TopWord,
                    rightWord = c.RightWord,
                    bottomWord = c.BottomWord,
                    leftWord = c.LeftWord,
                    rotation = c.Rotation
                }).ToList(),
                guessedPositions = response.GuessingState.GuessedPositions.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => kvp.Value == null ? null : new
                    {
                        cardId = kvp.Value.CardId,
                        topWord = kvp.Value.TopWord,
                        rightWord = kvp.Value.RightWord,
                        bottomWord = kvp.Value.BottomWord,
                        leftWord = kvp.Value.LeftWord,
                        rotation = kvp.Value.Rotation
                    }
                ),
                correctlyPlacedPositions = response.GuessingState.CorrectlyPlacedPositions.Select(p => p.ToString()).ToList(),
                remainingAttempts = response.GuessingState.RemainingAttempts,
                currentBoardClues = response.GuessingState.CurrentBoardClues.Select(c => new
                {
                    direction = c.Direction.ToString(),
                    text = c.Text
                }).ToList()
            },
            players = response.Players.Select(p => new
            {
                playerId = p.PlayerId.Value,
                name = p.Name,
                board = new
                {
                    top = new
                    {
                        direction = p.Board.Top.Direction.ToString(),
                        hasCard = p.Board.Top.HasCard,
                        isGuessed = p.Board.Top.IsGuessed,
                        clueLabel = p.Board.Top.ClueLabel,
                        expectedWord = p.Board.Top.ExpectedWord,
                        card = p.Board.Top.Card == null ? null : new
                        {
                            cardId = p.Board.Top.Card.CardId,
                            topWord = p.Board.Top.Card.TopWord,
                            rightWord = p.Board.Top.Card.RightWord,
                            bottomWord = p.Board.Top.Card.BottomWord,
                            leftWord = p.Board.Top.Card.LeftWord,
                            rotation = p.Board.Top.Card.Rotation
                        }
                    },
                    right = new
                    {
                        direction = p.Board.Right.Direction.ToString(),
                        hasCard = p.Board.Right.HasCard,
                        isGuessed = p.Board.Right.IsGuessed,
                        clueLabel = p.Board.Right.ClueLabel,
                        expectedWord = p.Board.Right.ExpectedWord,
                        card = p.Board.Right.Card == null ? null : new
                        {
                            cardId = p.Board.Right.Card.CardId,
                            topWord = p.Board.Right.Card.TopWord,
                            rightWord = p.Board.Right.Card.RightWord,
                            bottomWord = p.Board.Right.Card.BottomWord,
                            leftWord = p.Board.Right.Card.LeftWord,
                            rotation = p.Board.Right.Card.Rotation
                        }
                    },
                    bottom = new
                    {
                        direction = p.Board.Bottom.Direction.ToString(),
                        hasCard = p.Board.Bottom.HasCard,
                        isGuessed = p.Board.Bottom.IsGuessed,
                        clueLabel = p.Board.Bottom.ClueLabel,
                        expectedWord = p.Board.Bottom.ExpectedWord,
                        card = p.Board.Bottom.Card == null ? null : new
                        {
                            cardId = p.Board.Bottom.Card.CardId,
                            topWord = p.Board.Bottom.Card.TopWord,
                            rightWord = p.Board.Bottom.Card.RightWord,
                            bottomWord = p.Board.Bottom.Card.BottomWord,
                            leftWord = p.Board.Bottom.Card.LeftWord,
                            rotation = p.Board.Bottom.Card.Rotation
                        }
                    },
                    left = new
                    {
                        direction = p.Board.Left.Direction.ToString(),
                        hasCard = p.Board.Left.HasCard,
                        isGuessed = p.Board.Left.IsGuessed,
                        clueLabel = p.Board.Left.ClueLabel,
                        expectedWord = p.Board.Left.ExpectedWord,
                        card = p.Board.Left.Card == null ? null : new
                        {
                            cardId = p.Board.Left.Card.CardId,
                            topWord = p.Board.Left.Card.TopWord,
                            rightWord = p.Board.Left.Card.RightWord,
                            bottomWord = p.Board.Left.Card.BottomWord,
                            leftWord = p.Board.Left.Card.LeftWord,
                            rotation = p.Board.Left.Card.Rotation
                        }
                    }
                }
            }).ToList()
        };
        return Results.Ok(result);
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
})
.WithName("GetGameState");

app.MapPost("/api/games/{gameId:guid}/start", async (Guid gameId, IStartWritingPhaseUseCase useCase, CancellationToken ct) =>
{
    try
    {
        var response = await useCase.Handle(new StartWritingPhase.Request(new GameId(gameId)), ct);
        return Results.Ok(new { phase = response.Phase.ToString() });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
})
.WithName("StartWritingPhase");

app.MapPost("/api/games/{gameId:guid}/clues", async (Guid gameId, SetClueRequest? request, ISetClueUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId) || string.IsNullOrWhiteSpace(request?.Direction) || string.IsNullOrWhiteSpace(request?.ClueText))
    {
        return Results.BadRequest(new { message = "PlayerId, Direction, and ClueText are required" });
    }

    try
    {
        var direction = Enum.Parse<Direction>(request.Direction);
        var response = await useCase.Handle(new SetClue.Request(new GameId(gameId), new PlayerId(Guid.Parse(request.PlayerId)), direction, request.ClueText), ct);
        return Results.Ok(new { message = "Clue saved successfully" });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
})
.WithName("SetClue");

app.MapPost("/api/games/{gameId:guid}/submit-board", async (Guid gameId, SubmitBoardRequest? request, ISubmitBoardUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
    {
        return Results.BadRequest(new { message = "PlayerId is required" });
    }

    try
    {
        await useCase.Handle(new SubmitBoard.Request(new GameId(gameId), new PlayerId(Guid.Parse(request.PlayerId))), ct);
        return Results.Ok(new { message = "Board submitted successfully" });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (InvalidOperationInPhaseException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("SubmitBoard");

app.MapPost("/api/games/{gameId:guid}/start-guessing", async (Guid gameId, IStartGuessingPhaseUseCase useCase, CancellationToken ct) =>
{
    try
    {
        var response = await useCase.Handle(new StartGuessingPhase.Request(new GameId(gameId)), ct);
        return Results.Ok(new { phase = response.Phase.ToString(), currentBoardOwner = response.CurrentBoardOwner.Value });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (InvalidOperationInPhaseException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("StartGuessingPhase");

app.MapPost("/api/games/{gameId:guid}/place-guessing-card", async (Guid gameId, PlaceGuessingCardRequest? request, IPlaceGuessingCardUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
        return Results.BadRequest(new { message = "PlayerId is required" });

    try
    {
        var position = Enum.Parse<BoardPosition>(request.Position);
        await useCase.Handle(new PlaceGuessingCard.Request(
            new GameId(gameId),
            new PlayerId(Guid.Parse(request.PlayerId)),
            request.OutsideCardIndex,
            position
        ), ct);
        return Results.Ok(new { message = "Card placed successfully" });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("PlaceGuessingCard");

app.MapPost("/api/games/{gameId:guid}/rotate-board-card", async (Guid gameId, RotateBoardCardRequest? request, IRotateBoardCardUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
        return Results.BadRequest(new { message = "PlayerId is required" });

    try
    {
        var position = Enum.Parse<BoardPosition>(request.Position);
        await useCase.Handle(new RotateBoardCard.Request(
            new GameId(gameId),
            new PlayerId(Guid.Parse(request.PlayerId)),
            position
        ), ct);
        return Results.Ok(new { message = "Card rotated successfully" });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("RotateBoardCard");

app.MapPost("/api/games/{gameId:guid}/rotate-outside-card", async (Guid gameId, RotateOutsideCardRequest? request, IRotateOutsideCardUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
        return Results.BadRequest(new { message = "PlayerId is required" });

    try
    {
        await useCase.Handle(new RotateOutsideCard.Request(
            new GameId(gameId),
            new PlayerId(Guid.Parse(request.PlayerId)),
            request.OutsideCardIndex
        ), ct);
        return Results.Ok(new { message = "Card rotated successfully" });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("RotateOutsideCard");

app.MapPost("/api/games/{gameId:guid}/validate-guessing-board", async (Guid gameId, ValidateGuessingBoardRequest? request, IValidateGuessingBoardUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
        return Results.BadRequest(new { message = "PlayerId is required" });

    try
    {
        var response = await useCase.Handle(new ValidateGuessingBoard.Request(
            new GameId(gameId),
            new PlayerId(Guid.Parse(request.PlayerId))
        ), ct);
        return Results.Ok(new
        {
            correctPositions = response.CorrectPositions.Select(p => p.ToString()).ToList(),
            incorrectPositions = response.IncorrectPositions.Select(p => p.ToString()).ToList(),
            remainingAttempts = response.RemainingAttempts,
            isComplete = response.IsComplete,
            shouldMoveToNext = response.ShouldMoveToNext
        });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("ValidateGuessingBoard");

app.MapFallbackToFile("index.html");

app.Run();

// Request DTOs for API
record CreateGameRequest(string? Language);
record JoinGameRequest(string PlayerName);
record SetClueRequest(string PlayerId, string Direction, string ClueText);
record SubmitBoardRequest(string PlayerId);
record PlaceGuessingCardRequest(string PlayerId, int OutsideCardIndex, string Position);
record RotateBoardCardRequest(string PlayerId, string Position);
record RotateOutsideCardRequest(string PlayerId, int OutsideCardIndex);
record ValidateGuessingBoardRequest(string PlayerId);