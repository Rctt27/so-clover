using SoClover.Domain;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Boards;
using SoClover.UseCases.Games;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddSingleton<IGameRepository, InMemoryGameRepository>();
builder.Services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
builder.Services.AddSingleton<IWordDictionary, InMemoryWordDictionary>();

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
app.MapPost("/api/games", async (ICreateGameUseCase useCase, CancellationToken ct) =>
{
    var response = await useCase.Handle(new CreateGame.Request(), ct);
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

app.MapFallbackToFile("index.html");

app.Run();