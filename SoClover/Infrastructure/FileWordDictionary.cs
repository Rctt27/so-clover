using System.Collections.Concurrent;
using SoClover.Domain;

namespace SoClover.Infrastructure;

public sealed class FileWordDictionary : IWordDictionary
{
    private readonly string _dictionaryPath;
    private readonly ConcurrentDictionary<string, string> _resolvedFileByNormName = new();
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

    public async Task<IReadOnlyList<string>> GetAllWordsAsync(string language, CancellationToken ct = default)
    {
        var words = await GetOrLoadDictionaryAsync(language, ct);
        return words;
    }

    private async Task<List<string>> GetOrLoadDictionaryAsync(string language, CancellationToken ct)
    {
        if (_cachedDictionaries.TryGetValue(language, out var cached))
            return cached;

        // Resolve the dictionary file path in a way that is case- and accent-insensitive.
        // This avoids failures on Linux where filenames are case-sensitive and may contain diacritics (e.g., "Français_OFF.txt").
        var filePath = ResolveDictionaryFilePath(language);

        if (filePath is null)
        {
            // Optional fallback: try English when requested language is missing
            var fallback = ResolveDictionaryFilePath("English");
            if (fallback is null)
            {
                var available = GetAvailableLanguages();
                throw new FileNotFoundException($"Dictionary file for language '{language}' not found under '{_dictionaryPath}'. Available: {string.Join(", ", available)}");
            }
            filePath = fallback;
        }

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

    private string? ResolveDictionaryFilePath(string language)
    {
        if (string.IsNullOrWhiteSpace(language)) return null;
        var key = Normalize(language);

        if (_resolvedFileByNormName.TryGetValue(key, out var cachedPath))
        {
            return cachedPath;
        }

        if (!Directory.Exists(_dictionaryPath))
        {
            return null;
        }

        var files = Directory.EnumerateFiles(_dictionaryPath, "*.txt", SearchOption.TopDirectoryOnly).ToList();
        foreach (var f in files)
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var norm = Normalize(name);
            _resolvedFileByNormName.TryAdd(norm, f);
        }

        if (_resolvedFileByNormName.TryGetValue(key, out var resolved))
        {
            return resolved;
        }

        // Second pass: try partial match for languages like "Français" matching "Français_OFF"
        foreach (var kv in _resolvedFileByNormName)
        {
            if (kv.Key.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value;
            }
        }

        return null;
    }

    private IEnumerable<string> GetAvailableLanguages()
    {
        if (!Directory.Exists(_dictionaryPath)) yield break;
        foreach (var f in Directory.EnumerateFiles(_dictionaryPath, "*.txt", SearchOption.TopDirectoryOnly))
        {
            yield return Path.GetFileNameWithoutExtension(f);
        }
    }

    private static string Normalize(string input) => TextNormalizer.Normalize(input);
}
