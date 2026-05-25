using SoClover.Domain;

namespace SoClover.Tests.Helpers;

public sealed class TestWordDictionary : IWordDictionary
{
    public Task<IReadOnlyList<string>> GetRandomWordsAsync(string language, int count, CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<string>)Enumerable.Range(0, count).Select(i => $"Word{i}").ToList());

    public Task<IReadOnlyList<string>> GetAllWordsAsync(string language, CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<string>)new List<string> { "Word1", "Word2" });
}
