using SoClover.Domain;
using SoClover.Domain.Validation;
using SoClover.UseCases.Abstractions;
using SoClover.UseCases.Errors;

namespace SoClover.UseCases.Gameplay;

public interface IValidateClueUseCase : IUseCase<ValidateClue.Request, ValidateClue.Response> { }

public static class ValidateClue
{
    public readonly record struct Request(GameId GameId, PlayerId PlayerId, Direction Direction, string ClueText);
    public readonly record struct Response(ClueValidationResult Validation);

    public sealed class Handler : IValidateClueUseCase
    {
        private readonly IGameRepository _repo;
        private readonly IClueValidatorFactory _validatorFactory;

        public Handler(IGameRepository repo, IClueValidatorFactory validatorFactory)
        {
            _repo = repo;
            _validatorFactory = validatorFactory;
        }

        public async Task<Response> Handle(Request request, CancellationToken ct = default)
        {
            var game = await _repo.Get(request.GameId, ct) ?? throw new GameNotFoundException(request.GameId);
            var player = game.Players.FirstOrDefault(p => p.Id == request.PlayerId)
                         ?? throw new PlayerNotFoundException(request.PlayerId);
            var validator = _validatorFactory.GetFor(game.Language, game.SemanticClueCheckEnabled);

            // Guard: ClueText.Create will throw on empty/long — we handle empty as "valid" to avoid 400 on blank input
            if (string.IsNullOrWhiteSpace(request.ClueText))
                return new Response(ClueValidationResult.Valid());

            var result = validator.Validate(request.ClueText, request.Direction, player.Board);
            return new Response(result);
        }
    }
}