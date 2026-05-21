using System.Collections.Concurrent;

namespace SoClover.Infrastructure.AI.Prompts;

public sealed class FilePromptLoader
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public ParsedPromptSections Load(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
            throw new FileNotFoundException($"Prompt file not found: {path}", path);

        var lastWrite = info.LastWriteTimeUtc;
        if (_cache.TryGetValue(path, out var entry) && entry.LastWriteTimeUtc == lastWrite)
            return entry.Sections;

        var content = File.ReadAllText(path);
        var sections = Parse(content);
        _cache[path] = new CacheEntry(lastWrite, sections);
        return sections;
    }

    private static ParsedPromptSections Parse(string content)
    {
        var startIndex = 0;
        int? version = null;
        var lines = content.Split('\n');
        if (lines.Length > 0 && lines[0].TrimEnd('\r') == "---")
        {
            for (var i = 1; i < lines.Length; i++)
            {
                var raw = lines[i].TrimEnd('\r');
                if (raw == "---")
                {
                    startIndex = i + 1;
                    break;
                }
                var colon = raw.IndexOf(':');
                if (colon > 0)
                {
                    var key = raw[..colon].Trim();
                    if (key.Equals("version", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = raw[(colon + 1)..].Trim();
                        if (int.TryParse(value, out var parsed))
                            version = parsed;
                    }
                }
            }
        }

        string? currentSection = null;
        var system = new System.Text.StringBuilder();
        var user = new System.Text.StringBuilder();
        var retry = new System.Text.StringBuilder();

        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                currentSection = line[2..].Trim();
                continue;
            }

            switch (currentSection)
            {
                case "SYSTEM": system.AppendLine(line); break;
                case "USER": user.AppendLine(line); break;
                case "RETRY_FEEDBACK": retry.AppendLine(line); break;
            }
        }

        return new ParsedPromptSections(
            system.ToString(),
            user.ToString(),
            retry.ToString(),
            version);
    }

    private sealed record CacheEntry(DateTime LastWriteTimeUtc, ParsedPromptSections Sections);
}

public readonly record struct ParsedPromptSections(
    string System,
    string User,
    string RetryFeedback,
    int? Version);
