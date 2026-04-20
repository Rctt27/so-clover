using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Gameplay;

public interface ISetClueUseCase : IUseCase<SetClue.Request, SetClue.Response> { }

public static class SetClue
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId, Direction Direction, string ClueText);
    public readonly record struct Response(ClueValidationResult Validation);

    public sealed class Handler : ISetClueUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IEventPublisher _events;
        private readonly IClueValidatorFactory _validatorFactory;

        public Handler(IGameRepository repo, IEventPublisher events, IClueValidatorFactory validatorFactory)
        {
            _repo = repo;
            _events = events;
            _validatorFactory = validatorFactory;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);
            var validator = _validatorFactory.GetFor(game.Language, game.SemanticClueCheckEnabled);
            var result = game.SetClue(request.PlayerId, request.Direction, request.ClueText, validator);

            await _repo.Save(game, ct);

            return new Response(result);
        }
    }
}