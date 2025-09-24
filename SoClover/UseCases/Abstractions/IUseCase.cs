namespace SoClover.UseCases.Abstractions;

public interface IUseCase<in TRequest, TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken ct = default);
}
