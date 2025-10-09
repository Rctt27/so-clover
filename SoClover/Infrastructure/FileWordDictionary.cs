using System.Collections.Concurrent;
using SoClover.Domain;

namespace SoClover.Infrastructure;

public sealed class FileWordDictionary : IWordDictionary
{
    private readonly string _dictionaryPath;
    private readonly ConcurrentDictionary<string, List<string>> _cachedDictionaries = new();
    private readonly Random _random = new();

    public FileWordDictionary(string dictionaryPath)
    {
        _dictionaryPath = dictionaryPath ?? throw new ArgumentNullException(nameof(dictionaryPath));
    }

    public async Task<IReadOnlyList<string>> GetRandomWordsAsync(string language, int count, CancellationToken ct = default)
    {
        var words = await GetOrLoadDictionaryAsync(language, ct);

        if (words.Count < count)
            throw new InvalidOperationException($"Dictionary for language '{language}' has only {words.Count} words, but {count} were requested");

        var result = new List<string>(count);
        var availableIndices = Enumerable.Range(0, words.Count).ToList();

        lock (_random)
        {
            for (int i = 0; i < count; i++)
            {
                var randomIndex = _random.Next(availableIndices.Count);
                var wordIndex = availableIndices[randomIndex];
                result.Add(words[wordIndex]);
                availableIndices.RemoveAt(randomIndex);
            }
        }

        return result;
    }

    private async Task<List<string>> GetOrLoadDictionaryAsync(string language, CancellationToken ct)
    {
        if (_cachedDictionaries.TryGetValue(language, out var cached))
            return cached;

        var filePath = Path.Combine(_dictionaryPath, $"{language.ToLowerInvariant()}.txt");

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Dictionary file for language '{language}' not found at: {filePath}");

        var lines = await File.ReadAllLinesAsync(filePath, ct);
        var words = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .Where(word => word.Length > 0 && word.Length <= 32)
            .ToList();

        if (words.Count == 0)
            throw new InvalidOperationException($"Dictionary file for language '{language}' is empty or contains no valid words");

        _cachedDictionaries[language] = words;
        return words;
    }
}
