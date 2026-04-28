using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json.Serialization;
using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;
using SoClover.UseCases.Gameplay;
using SoClover.UseCases.GameLogics;

var builder = WebApplication.CreateBuilder(args);

// Configure game defaults from appsettings.json
builder.Services.Configure<GameDefaultsOptions>(
    builder.Configuration.GetSection("GameDefaults"));

// Infrastructure
// Configure PostgreSQL DbContext (prod-ready)
var connectionString = builder.Configuration.GetConnectionString("GameDb") ?? Environment.GetEnvironmentVariable("DATABASE_URL") ?? "Host=localhost;Database=soclover;Username=postgres;Password=postgres";
builder.Services.AddDbContext<SoClover.Infrastructure.Persistence.GameDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

#if DEBUG
builder.Services.AddSingleton<IGameRepository, InMemoryGameRepository>();
#else
builder.Services.AddScoped<IGameRepository, SoClover.Infrastructure.Persistence.EfGameRepository>();
#endif

// Event publisher will be decorated by SignalR broadcaster (see below)
builder.Services.AddSingleton<InMemoryEventPublisher>();
builder.Services.AddSingleton<IWordDictionary>(sp =>
    new FileWordDictionary(Path.Combine(builder.Environment.ContentRootPath, "Infrastructure", "Dictionaries")));
builder.Services.AddSingleton<IWordsPoolCache, InMemoryWordsPoolCache>();

// Time and settings providers
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IGameSettingsProvider, ConfigurationGameSettingsProvider>();

// Background process manager
builder.Services.AddHostedService<GameProcessManager>(); // manages deadlines for lobby, writing, guessing, scoring
builder.Services.AddHostedService<CleanupHostedService>(); // periodic cleanup of completed games

// Domain services (CardFactory is now created internally by Game)

// Use cases
builder.Services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
builder.Services.AddTransient<IDeleteGameUseCase, DeleteGame.Handler>();
builder.Services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
builder.Services.AddTransient<IUpdateGameSettingsUseCase, UpdateGameSettings.Handler>();
builder.Services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
builder.Services.AddTransient<ISetClueUseCase, SetClue.Handler>();
builder.Services.AddTransient<IValidateClueUseCase, ValidateClue.Handler>();
builder.Services.AddSingleton<SoClover.Domain.Validation.IClueValidatorFactory, SoClover.Infrastructure.Validation.ClueValidatorFactory>();
builder.Services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
builder.Services.AddTransient<IGuessUseCase, Guess.Handler>();
builder.Services.AddTransient<IPlaceCardToGuessUseCase, PlaceCardToGuess.Handler>();
builder.Services.AddTransient<IGetGameStateUseCase, GetGameState.Handler>();
builder.Services.AddTransient<ISubmitBoardUseCase, SubmitBoard.Handler>();
builder.Services.AddTransient<IPlaceGuessingCardUseCase, PlaceGuessingCard.Handler>();
builder.Services.AddTransient<ISwapGuessingCardsUseCase, SwapGuessingCards.Handler>();
builder.Services.AddTransient<ISwapOutsidePoolCardsUseCase, SwapOutsidePoolCards.Handler>();
builder.Services.AddTransient<IReturnGuessingCardUseCase, ReturnGuessingCard.Handler>();
builder.Services.AddTransient<IRotateCardUseCase, RotateCard.Handler>();
builder.Services.AddTransient<IRotateBoardUseCase, RotateBoard.Handler>();
builder.Services.AddTransient<IValidateGuessingBoardUseCase, ValidateGuessingBoard.Handler>();
builder.Services.AddTransient<IMoveToNextBoardUseCase, MoveToNextBoard.Handler>();
builder.Services.AddTransient<IGetScoringUseCase, GetScoring.Handler>();
builder.Services.AddTransient<ICompleteGameUseCase, CompleteGame.Handler>();
builder.Services.AddTransient<ILeaveGameUseCase, LeaveGame.Handler>();
builder.Services.AddTransient<IKickPlayerUseCase, KickPlayer.Handler>();
builder.Services.AddTransient<ICreateAIPlayerUseCase, CreateAIPlayer.Handler>();
builder.Services.AddTransient<IDisconnectPlayerUseCase, DisconnectPlayer.Handler>();
builder.Services.AddSingleton<SoClover.RealTime.IConnectionTracker, SoClover.RealTime.SignalRConnectionTracker>();

// Add SignalR (backplane ready, but optional)
// Note: We keep Redis backplane optional to avoid hard dependency. When you're ready,
// add Microsoft.AspNetCore.SignalR.StackExchangeRedis package and uncomment the AddStackExchangeRedis line.
var signalRBuilder = builder.Services.AddSignalR()
    .AddJsonProtocol(options => 
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.ConfigureHttpJsonOptions(options => 
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
// var redisCs = builder.Configuration.GetConnectionString("Redis");
// if (!string.IsNullOrWhiteSpace(redisCs))
// {
//     signalRBuilder.AddStackExchangeRedis(redisCs);
// }

// Decorate IEventPublisher with a SignalR-based broadcaster that forwards domain events to clients
// while still invoking the inner in-memory publisher (console log).
builder.Services.AddSingleton<IEventPublisher>(sp =>
{
    var inner = sp.GetRequiredService<InMemoryEventPublisher>();
    var hub = sp.GetRequiredService<IHubContext<SoClover.RealTime.GameHub>>();
    var getState = sp.GetRequiredService<IGetGameStateUseCase>();
    return new SoClover.Infrastructure.SignalREventPublisher(inner, hub, getState);
});

// System HMAC validator (for optional system-to-system HTTP calls)
builder.Services.AddSingleton<SoClover.Infrastructure.IHmacValidator, SoClover.Infrastructure.HmacValidator>();

// CORS : permissif en dev, restreint au domaine en production
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins("https://soclover.couttet.fr")
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

var app = builder.Build();

app.UseCors();
app.UseStaticFiles();

// Hubs
app.MapHub<SoClover.RealTime.GameHub>("/hubs/game");

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
        var response = await useCase.Handle(
            new JoinGame.Request(new GameId(gameId), request.PlayerName, request.ReplaceExisting), ct);

        if (response.IsConflict)
        {
            return Results.Conflict(new
            {
                message = "A player with this name already exists",
                existingPlayerId = response.ExistingPlayerId?.Value
            });
        }

        return Results.Ok(new { playerId = response.PlayerId.Value });
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
.WithName("JoinGame");

app.MapPost("/api/games/{gameId:guid}/leave", async (Guid gameId, LeaveGameRequest? request, ILeaveGameUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
    {
        return Results.BadRequest(new { message = "PlayerId is required" });
    }

    if (!Guid.TryParse(request.PlayerId, out var playerGuid) || playerGuid == Guid.Empty)
    {
        return Results.BadRequest(new { message = "PlayerId must be a valid GUID" });
    }

    try
    {
        var response = await useCase.Handle(new LeaveGame.Request(new GameId(gameId), new PlayerId(playerGuid)), ct);
        return Results.Ok(new { success = response.Success });
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
.WithName("LeaveGame");

app.MapPost("/api/games/{gameId:guid}/kick", async (Guid gameId, KickPlayerRequest? request, IKickPlayerUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId) || string.IsNullOrWhiteSpace(request?.AdminPlayerId))
    {
        return Results.BadRequest(new { message = "PlayerId and AdminPlayerId are required" });
    }

    if (!Guid.TryParse(request.PlayerId, out var targetGuid) || !Guid.TryParse(request.AdminPlayerId, out var adminGuid))
    {
        return Results.BadRequest(new { message = "IDs must be valid GUIDs" });
    }

    try
    {
        var response = await useCase.Handle(
            new KickPlayer.Request(new GameId(gameId), new PlayerId(targetGuid), new PlayerId(adminGuid)), ct);
        return Results.Ok(new { success = response.Success, kickedPlayerName = response.KickedPlayerName });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (UnauthorizedAccessException)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
    catch (PlayerNotFoundException)
    {
        return Results.NotFound(new { message = "Player not found" });
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
.WithName("KickPlayer");

app.MapPost("/api/games/{gameId:guid}/ai-players", async (
    Guid gameId,
    CreateAIPlayerRequest? request,
    ICreateAIPlayerUseCase useCase,
    CancellationToken ct) =>
{
    if (request is null
        || string.IsNullOrWhiteSpace(request.AdminPlayerId)
        || string.IsNullOrWhiteSpace(request.PlayerName))
    {
        return Results.BadRequest(new { message = "AdminPlayerId and PlayerName are required" });
    }

    if (!Guid.TryParse(request.AdminPlayerId, out var adminGuid) || adminGuid == Guid.Empty)
    {
        return Results.BadRequest(new { message = "AdminPlayerId must be a valid non-empty GUID" });
    }

    try
    {
        var response = await useCase.Handle(new CreateAIPlayer.Request(
            new GameId(gameId),
            new PlayerId(adminGuid),
            request.PlayerName,
            request.Model,
            request.Temperature), ct);

        return Results.Ok(new { playerId = response.PlayerId.Value });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (UnauthorizedAccessException)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
    catch (MaxAIPlayersReachedException ex)
    {
        return Results.Conflict(new
        {
            message = ex.Message,
            currentCount = ex.CurrentCount,
            max = ex.Max
        });
    }
    catch (InvalidOperationInPhaseException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (PlayerNameEmptyException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (PlayerNameTooLongException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("CreateAIPlayer");

app.MapPut("/api/games/{gameId:guid}/settings", async (Guid gameId, UpdateGameSettingsRequest? request, IUpdateGameSettingsUseCase useCase, CancellationToken ct) =>
{
    if (request is null)
    {
        return Results.BadRequest(new { message = "Body is required" });
    }

    if (string.IsNullOrWhiteSpace(request.PlayerId))
    {
        return Results.BadRequest(new { message = "PlayerId is required" });
    }

    if (!Guid.TryParse(request.PlayerId, out var playerGuid))
    {
        return Results.BadRequest(new { message = "PlayerId must be a valid GUID" });
    }

    if (playerGuid == Guid.Empty)
    {
        return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
    }

    if (string.IsNullOrWhiteSpace(request.Language))
    {
        return Results.BadRequest(new { message = "Language is required" });
    }

    try
    {
        var response = await useCase.Handle(
            new UpdateGameSettings.Request(
                new GameId(gameId),
                new PlayerId(playerGuid),
                request.Language.Trim(),
                request.CluesDuration,
                request.GuessDuration,
                request.SemanticClueCheckEnabled),
            ct);
        return Results.Ok(new
        {
            language = response.Language,
            cluesDuration = response.CluesDurationSeconds,
            guessDuration = response.GuessDurationSeconds,
            semanticClueCheckEnabled = response.SemanticClueCheckEnabled
        });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (UnauthorizedAccessException)
    {
        // Avoid Results.Forbid() since no authentication is configured; return 403 directly
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
    catch (InvalidOperationInPhaseException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (FileNotFoundException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("UpdateGameSettings");

app.MapGet("/api/games/{gameId:guid}/state", async (Guid gameId, string? playerId, bool includeSecrets, IGetGameStateUseCase useCase, CancellationToken ct) =>
{
    try
    {
        // Include secrets for the requesting player's own board to allow clients to render their words
        PlayerId? requestingPlayerId = null;
        if (!string.IsNullOrWhiteSpace(playerId) && Guid.TryParse(playerId, out var pid))
        {
            requestingPlayerId = new PlayerId(pid);
        }

        var response = await useCase.Handle(new GetGameState.Request(new GameId(gameId), includeSecrets, requestingPlayerId), ct);
        var result = new
        {
            gameId = response.GameId,
            language = response.Language,
            cluesDurationSecondsOverride = response.CluesDurationSecondsOverride,
            guessDurationSecondsOverride = response.GuessDurationSecondsOverride,
            semanticClueCheckEnabled = response.SemanticClueCheckEnabled,
            phase = response.Phase.ToString(),
            phaseEndsAtUtc = response.PhaseEndsAtUtc,
            adminPlayerId = response.AdminPlayerId?.ToString(),
            guessingState = response.GuessingState == null ? null : new
            {
                currentBoardOwnerId = response.GuessingState.CurrentBoardOwnerId,
                currentBoardOwnerName = response.GuessingState.CurrentBoardOwnerName,
                outsideCards = response.GuessingState.OutsideCards.Select(c => c == null ? null : new
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
                playerId = p.PlayerId,
                name = p.Name,
                isAI = p.IsAI,
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
    catch (DisconnectedPlayersException ex)
    {
        return Results.BadRequest(new { message = ex.Message, disconnectedPlayers = ex.PlayerNames });
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
        var parsed = Guid.Parse(request.PlayerId);
        if (parsed == Guid.Empty) return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
        var response = await useCase.Handle(new SetClue.Request(new GameId(gameId), new PlayerId(parsed), direction, request.ClueText), ct);
        if (!response.Validation.IsValid)
        {
            return Results.BadRequest(new
            {
                message = "Clue rejected by semantic validation",
                errors = response.Validation.Errors.Select(e => new
                {
                    rule = e.Rule.ToString(),
                    cardWord = e.CardWord,
                    conflictingDirection = e.ConflictingDirection?.ToString()
                }).ToArray()
            });
        }
        return Results.Ok(new { message = "Clue saved successfully" });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (InvalidClueException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("SetClue");

app.MapPost("/api/games/{gameId:guid}/clues/validate", async (Guid gameId, SetClueRequest? request, IValidateClueUseCase useCase, CancellationToken ct) =>
    {
        if (request is null || string.IsNullOrWhiteSpace(request.PlayerId) || string.IsNullOrWhiteSpace(request.Direction))
            return Results.BadRequest(new { message = "PlayerId and Direction are required" });

        try
        {
            var direction = Enum.Parse<Direction>(request.Direction);
            var parsed = Guid.Parse(request.PlayerId);
            if (parsed == Guid.Empty) return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
            var response = await useCase.Handle(new ValidateClue.Request(new GameId(gameId), new PlayerId(parsed), direction, request.ClueText ?? string.Empty), ct);
            return Results.Ok(new
            {
                isValid = response.Validation.IsValid,
                errors = response.Validation.Errors.Select(e => new
                {
                    rule = e.Rule.ToString(),
                    cardWord = e.CardWord,
                    conflictingDirection = e.ConflictingDirection?.ToString()
                }).ToArray()
            });
        }
        catch (GameNotFoundException)
        {
            return Results.NotFound(new { message = "Game not found" });
        }
        catch (PlayerNotFoundException)
        {
            return Results.NotFound(new { message = "Player not found" });
        }
    })
    .WithName("ValidateClue");

app.MapPost("/api/games/{gameId:guid}/submit-board", async (Guid gameId, SubmitBoardRequest? request, ISubmitBoardUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
    {
        return Results.BadRequest(new { message = "PlayerId is required" });
    }

    try
    {
        var parsed = Guid.Parse(request.PlayerId);
        if (parsed == Guid.Empty) return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
        await useCase.Handle(new SubmitBoard.Request(new GameId(gameId), new PlayerId(parsed)), ct);
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
    catch (NoHumanGuesserException ex)
    {
        return Results.Conflict(new { message = ex.Message });
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
        var parsed = Guid.Parse(request.PlayerId);
        if (parsed == Guid.Empty) return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
        await useCase.Handle(new PlaceGuessingCard.Request(
            new GameId(gameId),
            new PlayerId(parsed),
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
        var parsed = Guid.Parse(request.PlayerId);
        if (parsed == Guid.Empty) return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
        await useCase.Handle(new SwapGuessingCards.Request(
            new GameId(gameId),
            new PlayerId(parsed),
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

app.MapPost("/api/games/{gameId:guid}/swap-outside-pool-cards", async (Guid gameId, SwapOutsidePoolCardsRequest? request, ISwapOutsidePoolCardsUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
        return Results.BadRequest(new { message = "PlayerId is required" });

    try
    {
        var parsed = Guid.Parse(request.PlayerId);
        if (parsed == Guid.Empty) return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
        await useCase.Handle(new SwapOutsidePoolCards.Request(
            new GameId(gameId),
            new PlayerId(parsed),
            request.Index1,
            request.Index2
        ), ct);
        return Results.Ok(new { message = "Pool cards swapped successfully" });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("SwapOutsidePoolCards");

app.MapPost("/api/games/{gameId:guid}/return-guessing-card", async (Guid gameId, ReturnGuessingCardRequest? request, IReturnGuessingCardUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
        return Results.BadRequest(new { message = "PlayerId is required" });

    try
    {
        var position = Enum.Parse<BoardPosition>(request.Position);
        var parsed = Guid.Parse(request.PlayerId);
        if (parsed == Guid.Empty) return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
        await useCase.Handle(new ReturnGuessingCard.Request(
            new GameId(gameId),
            new PlayerId(parsed),
            position
        ), ct);
        return Results.Ok(new { message = "Card returned successfully" });
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
.WithName("ReturnGuessingCard");

app.MapPost("/api/games/{gameId:guid}/rotate-board", async (Guid gameId, RotateBoardRequest? request, IRotateBoardUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId) || !request.CumulativeRotation.HasValue)
    {
        return Results.BadRequest(new { message = "PlayerId and CumulativeRotation are required" });
    }

    try
    {
        var parsed = Guid.Parse(request.PlayerId);
        if (parsed == Guid.Empty) return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
        await useCase.Handle(new RotateBoard.Request(
            new GameId(gameId),
            new PlayerId(parsed),
            request.CumulativeRotation.Value
        ), ct);

        return Results.Ok(new { message = "Board rotated successfully" });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (InvalidOperationInPhaseException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("RotateBoard");

app.MapPost("/api/games/{gameId:guid}/rotate-card", async (Guid gameId, RotateCardRequest? request, IRotateCardUseCase useCase, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request?.PlayerId))
        return Results.BadRequest(new { message = "PlayerId is required" });

    try
    {
        int steps = request.Steps ?? (request.Direction?.ToLower() == "left" ? -1 : 1);

        // Determine if rotating board card or outside card
        if (!string.IsNullOrWhiteSpace(request.Position))
        {
            var position = Enum.Parse<BoardPosition>(request.Position);
            var parsed = Guid.Parse(request.PlayerId);
            if (parsed == Guid.Empty) return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
            await useCase.Handle(new RotateCard.Request(
                new GameId(gameId),
                new PlayerId(parsed),
                null,
                position,
                steps
            ), ct);
        }
        else if (request.OutsideCardIndex.HasValue)
        {
            var parsed = Guid.Parse(request.PlayerId);
            if (parsed == Guid.Empty) return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
            await useCase.Handle(new RotateCard.Request(
                new GameId(gameId),
                new PlayerId(parsed),
                request.OutsideCardIndex.Value,
                null,
                steps
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
    catch (InvalidOperationInPhaseException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
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
        var parsed = Guid.Parse(request.PlayerId);
        if (parsed == Guid.Empty) return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
        var response = await useCase.Handle(new ValidateGuessingBoard.Request(
            new GameId(gameId),
            new PlayerId(parsed)
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
        var parsed = Guid.Parse(request.PlayerId);
        if (parsed == Guid.Empty) return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
        var response = await useCase.Handle(new MoveToNextBoard.Request(
            new GameId(gameId),
            new PlayerId(parsed)
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
                wasGuessed = b.WasGuessed,
                isDisconnected = b.IsDisconnected
            }).ToList(),
            failedBoards = response.FailedBoards.Select(b => new
            {
                playerId = b.PlayerId,
                playerName = b.PlayerName,
                attempts = b.Attempts,
                durationSeconds = b.DurationSeconds,
                wasGuessed = b.WasGuessed,
                isDisconnected = b.IsDisconnected
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
        var parsed = Guid.Parse(request.PlayerId);
        if (parsed == Guid.Empty) return Results.BadRequest(new { message = "PlayerId must not be empty GUID" });
        var response = await useCase.Handle(new CompleteGame.Request(
            new GameId(gameId),
            new PlayerId(parsed)
        ), ct);
        return Results.Ok(new { phase = response.Phase.ToString() });
    }
    catch (GameNotFoundException)
    {
        return Results.NotFound(new { message = "Game not found" });
    }
    catch (UnauthorizedAccessException ex)
    {
        // Avoid Results.Forbid() since no authentication is configured; return 403 directly
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
    catch (InvalidOperationInPhaseException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("CompleteGame");

// Optional: System-only endpoint to force move to next board via HTTP, authenticated by HMAC
app.MapPost("/api/system/games/{gameId:guid}/move-to-next-board", async (
    Guid gameId,
    HttpRequest http,
    IMoveToNextBoardUseCase useCase,
    IHmacValidator validator,
    IConfiguration cfg,
    CancellationToken ct) =>
{
    var secret = cfg["SystemHmacSecret"] ?? Environment.GetEnvironmentVariable("SYSTEM_HMAC_SECRET");
    if (!validator.IsValid(http, secret))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        var response = await useCase.Handle(new MoveToNextBoard.Request(
            new GameId(gameId),
            default,
            SoClover.UseCases.Abstractions.InvocationOrigin.System
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
.WithName("System_MoveToNextBoard");

// List available dictionaries from Infrastructure/Dictionaries (*.txt)
app.MapGet("/api/dictionaries", (IWebHostEnvironment env) =>
{
    try
    {
        var dir = Path.Combine(env.ContentRootPath, "Infrastructure", "Dictionaries");
        if (!Directory.Exists(dir))
        {
            return Results.Ok(Array.Empty<object>());
        }

        var items = Directory.EnumerateFiles(dir, "*.txt")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new { key = name, name = name })
            .ToList();

        return Results.Ok(items);
    }
    catch
    {
        return Results.Ok(Array.Empty<object>());
    }
});

app.MapGet("/health", () => Results.Ok());

app.MapFallbackToFile("index.html");

app.Run();

// Request DTOs for API
record CreateGameRequest(string PlayerName, string? Language = null);
record JoinGameRequest(string PlayerName, bool ReplaceExisting = false);
record UpdateGameSettingsRequest(string PlayerId, string Language, int? CluesDuration, int? GuessDuration, bool? SemanticClueCheckEnabled);
record SetClueRequest(string PlayerId, string Direction, string ClueText);
record SubmitBoardRequest(string PlayerId);
record PlaceGuessingCardRequest(string PlayerId, int OutsideCardIndex, string Position);
record SwapGuessingCardsRequest(string PlayerId, string Position1, string Position2);
record SwapOutsidePoolCardsRequest(string PlayerId, int Index1, int Index2);
record ReturnGuessingCardRequest(string PlayerId, string Position);
record RotateBoardRequest(
    [property: JsonPropertyName("playerId")] string PlayerId, 
    [property: JsonPropertyName("cumulativeRotation")] int? CumulativeRotation);
record RotateCardRequest(string PlayerId, string? Position = null, int? OutsideCardIndex = null, string? Direction = null, int? Steps = null);
record ValidateGuessingBoardRequest(string PlayerId);
record MoveToNextBoardRequest(string PlayerId);
record CompleteGameRequest(string PlayerId);
record LeaveGameRequest(string PlayerId);
record KickPlayerRequest(string PlayerId, string AdminPlayerId);
record CreateAIPlayerRequest(string AdminPlayerId, string PlayerName, string? Model, double? Temperature);