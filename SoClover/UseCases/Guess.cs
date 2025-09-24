using SoClover.Domain;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Games;

public static class Guess
{
    public readonly record struct Request(GameId GameId, PlayerId OwnerId, Direction Direction, string Word);
    public readonly record struct Response(bool IsCorrect, string ExpectedWord);

    public sealed class Handler : IUseCase<Request, Response>
    {
        private readonly IGameRepository _repo;
        private readonly IEventPublisher _events;

        public Handler(IGameRepository repo, IEventPublisher events)
        {
            _repo = repo;
            _events = events;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);
            var result = game.Guess(request.OwnerId, request.Direction, request.Word);
            await _repo.Save(game, ct);
            await _events.Publish(new GuessSubmitted(game.Id, request.OwnerId, request.Direction, result.IsCorrect), ct);
            return new Response(result.IsCorrect, result.ExpectedWord);
        }
    }
}

public readonly record struct GuessSubmitted(GameId GameId, PlayerId OwnerId, Direction Direction, bool IsCorrect);
