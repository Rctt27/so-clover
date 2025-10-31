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

// Domain services (CardFactory is now created internally by Game)

// Use cases
builder.Services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
builder.Services.AddTransient<IDeleteGameUseCase, DeleteGame.Handler>();
builder.Services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
builder.Services.AddTransient<IUpdateGameSettingsUseCase, UpdateGameSettings.Handler>();
builder.Services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
builder.Services.AddTransient<ISetClueUseCase, SetClue.Handler>();
builder.Services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
builder.Services.AddTransient<IGuessUseCase, Guess.Handler>();
builder.Services.AddTransient<IPlaceCardUseCase, PlaceCard.Handler>();
builder.Services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
builder.Services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();
builder.Services.AddTransient<IPlaceGuessingCardUseCase, PlaceGuessingCard.Handler>();
builder.Services.AddTransient<ISwapGuessingCardsUseCase, SwapGuessingCards.Handler>();
builder.Services.AddTransient<IRotateCardUseCase, RotateCard.Handler>();
builder.Services.AddTransient<IValidateGuessingBoardUseCase, ValidateGuessingBoard.Handler>();
builder.Services.AddTransient<IMoveToNextBoardUseCase, MoveToNextBoard.Handler>();
builder.Services.AddTransient<IGetScoringUseCase, GetScoring.Handler>();
builder.Services.AddTransient<ICompleteGameUseCase, CompleteGame.Handler>();

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
    if (string.IsNullOrWhiteSpace(request?.PlayerName))
    {
        return Results.BadRequest(new { message = "Player name is required" });
    }

    var response = await useCase.Handle(new CreateGame.Request(request.PlayerName, request.Language), ct);
    return Results.Ok(new { gameId = response.GameId.Value, playerId = response.CreatorPlayerId.Value });
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

app.MapPut("/api/games/{gameId:guid}/settings", async (Guid gameId, UpdateGameSettingsRequest? request, IUpdateGameSettingsUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId) || string.IsNullOrWhiteSpace(request?.Language))
    {
        return Results.BadRequest(new { message = "PlayerId and Language are required" });
    }

    try
    {
        var response = await useCase.Handle(
            new UpdateGameSettings.Request(
                new GameId(gameId),
                new PlayerId(Guid.Parse(request.PlayerId)),
                request.Language),
            ct);
        return Results.Ok(new { language = response.Language });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Forbid();
    }
    catch (InvalidOperationInPhaseException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("UpdateGameSettings");

app.MapGet("/api/games/{gameId:guid}/state", async (Guid gameId, string? playerId, bool includeSecrets, IGetGameStateUseCase useCase, CancellationToken ct) =>
{
    try
    {
        var response = await useCase.Handle(new GetGameState.Request(new GameId(gameId), includeSecrets), ct);
        var result = new
        {
            gameId = response.GameId.Value,
            phase = response.Phase.ToString(),
            adminPlayerId = response.AdminPlayerId?.Value.ToString(),
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

app.MapPost("/api/games/{gameId:guid}/swap-guessing-cards", async (Guid gameId, SwapGuessingCardsRequest? request, ISwapGuessingCardsUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
        return Results.BadRequest(new { message = "PlayerId is required" });

    try
    {
        var position1 = Enum.Parse<BoardPosition>(request.Position1);
        var position2 = Enum.Parse<BoardPosition>(request.Position2);
        await useCase.Handle(new SwapGuessingCards.Request(
            new GameId(gameId),
            new PlayerId(Guid.Parse(request.PlayerId)),
            position1,
            position2
        ), ct);
        return Results.Ok(new { message = "Cards swapped successfully" });
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
.WithName("SwapGuessingCards");

app.MapPost("/api/games/{gameId:guid}/rotate-card", async (Guid gameId, RotateCardRequest? request, IRotateCardUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
        return Results.BadRequest(new { message = "PlayerId is required" });

    try
    {
        var rotateRight = request.Direction?.ToLower() != "left";

        // Determine if rotating board card or outside card
        if (!string.IsNullOrWhiteSpace(request.Position))
        {
            var position = Enum.Parse<BoardPosition>(request.Position);
            await useCase.Handle(new RotateCard.Request(
                new GameId(gameId),
                new PlayerId(Guid.Parse(request.PlayerId)),
                null,
                position,
                rotateRight
            ), ct);
        }
        else if (request.OutsideCardIndex.HasValue)
        {
            await useCase.Handle(new RotateCard.Request(
                new GameId(gameId),
                new PlayerId(Guid.Parse(request.PlayerId)),
                request.OutsideCardIndex.Value,
                null,
                rotateRight
            ), ct);
        }
        else
        {
            return Results.BadRequest(new { message = "Either Position or OutsideCardIndex must be provided" });
        }

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
.WithName("RotateCard");

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

app.MapPost("/api/games/{gameId:guid}/move-to-next-board", async (Guid gameId, MoveToNextBoardRequest? request, IMoveToNextBoardUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
        return Results.BadRequest(new { message = "PlayerId is required" });

    try
    {
        var response = await useCase.Handle(new MoveToNextBoard.Request(
            new GameId(gameId),
            new PlayerId(Guid.Parse(request.PlayerId))
        ), ct);
        return Results.Ok(new
        {
            phase = response.Phase.ToString(),
            nextBoardOwnerId = response.NextBoardOwnerId?.Value.ToString()
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
    catch (InvalidOperationInPhaseException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("MoveToNextBoard");

app.MapGet("/api/games/{gameId:guid}/scoring", async (Guid gameId, IGetScoringUseCase useCase, CancellationToken ct) =>
{
    try
    {
        var response = await useCase.Handle(new GetScoring.Request(new GameId(gameId)), ct);
        return Results.Ok(new
        {
            successfulBoards = response.SuccessfulBoards.Select(b => new
            {
                playerId = b.PlayerId,
                playerName = b.PlayerName,
                attempts = b.Attempts,
                durationSeconds = b.DurationSeconds,
                wasGuessed = b.WasGuessed
            }).ToList(),
            failedBoards = response.FailedBoards.Select(b => new
            {
                playerId = b.PlayerId,
                playerName = b.PlayerName,
                attempts = b.Attempts,
                durationSeconds = b.DurationSeconds,
                wasGuessed = b.WasGuessed
            }).ToList()
        });
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
.WithName("GetScoring");

app.MapPost("/api/games/{gameId:guid}/complete", async (Guid gameId, CompleteGameRequest? request, ICompleteGameUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
        return Results.BadRequest(new { message = "PlayerId is required" });

    try
    {
        var response = await useCase.Handle(new CompleteGame.Request(
            new GameId(gameId),
            new PlayerId(Guid.Parse(request.PlayerId))
        ), ct);
        return Results.Ok(new { phase = response.Phase.ToString() });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Forbid();
    }
    catch (InvalidOperationInPhaseException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("CompleteGame");

app.MapFallbackToFile("index.html");

app.Run();

// Request DTOs for API
record CreateGameRequest(string PlayerName, string? Language = null);
record JoinGameRequest(string PlayerName);
record UpdateGameSettingsRequest(string PlayerId, string Language);
record SetClueRequest(string PlayerId, string Direction, string ClueText);
record SubmitBoardRequest(string PlayerId);
record PlaceGuessingCardRequest(string PlayerId, int OutsideCardIndex, string Position);
record SwapGuessingCardsRequest(string PlayerId, string Position1, string Position2);
record RotateCardRequest(string PlayerId, string? Position = null, int? OutsideCardIndex = null, string? Direction = null);
record ValidateGuessingBoardRequest(string PlayerId);
record MoveToNextBoardRequest(string PlayerId);
record CompleteGameRequest(string PlayerId);