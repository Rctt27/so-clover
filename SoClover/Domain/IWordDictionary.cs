namespace SoClover.Domain;

public interface IWordDictionary
{
    Task<IReadOnlyList<string>> GetRandomWordsAsync(string language, int count, CancellationToken ct = default);
}
