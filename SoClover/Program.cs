using Microsoft.Extensions.DependencyInjection;
using SoClover.Infrastructure;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Boards;
using SoClover.UseCases.Games;

var services = new ServiceCollection();

// Infrastructure
services.AddSingleton<IGameRepository, InMemoryGameRepository>();
services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();

// Use cases
services.AddTransient<ICreateGameUseCase, CreateGame.Handler>();
services.AddTransient<IJoinGameUseCase, JoinGame.Handler>();
services.AddTransient<IStartWritingPhaseUseCase, StartWritingPhase.Handler>();
services.AddTransient<ISetClueUseCase, SetClue.Handler>();
services.AddTransient<IStartGuessingPhaseUseCase, StartGuessingPhase.Handler>();
services.AddTransient<IGuessUseCase, Guess.Handler>();
services.AddTransient<IPlaceCardUseCase, PlaceCard.Handler>();

// Build provider
var provider = services.BuildServiceProvider();

// Minimal demo of container working
var repo = provider.GetRequiredService<IGameRepository>();
var eventsPublisher = provider.GetRequiredService<IEventPublisher>();
Console.WriteLine("DI container initialized. Repo: {0}, Events: {1}", repo.GetType().Name, eventsPublisher.GetType().Name);