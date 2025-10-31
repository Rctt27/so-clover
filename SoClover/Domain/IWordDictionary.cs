namespace SoClover.Domain;

public interface IWordDictionary
{
    Task<IReadOnlyList<string>> GetRandomWordsAsync(string language, int count, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAllWordsAsync(string language, CancellationToken ct = default);
}
