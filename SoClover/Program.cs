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
builder.Services.AddSingleton<IWordDictionary, InMemoryWordDictionary>();

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
        return Results.Ok(new
        {
            gameId = response.GameId.Value,
            phase = response.Phase.ToString(),
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
        });
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

app.MapFallbackToFile("index.html");

app.Run();

// Request DTOs for API
record CreateGameRequest(string? Language);
record JoinGameRequest(string PlayerName);