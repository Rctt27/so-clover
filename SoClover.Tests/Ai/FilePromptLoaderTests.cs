using SoClover.Infrastructure.AI.Prompts;
using Xunit;

namespace SoClover.Tests.AI;

public sealed class FilePromptLoaderTests : IDisposable
{
    private readonly string _tempPath;

    public FilePromptLoaderTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"prompt-{Guid.NewGuid()}.md");
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath))
            File.Delete(_tempPath);
    }

    [Fact]
    public void Load_throws_when_file_missing()
    {
        var loader = new FilePromptLoader();

        Assert.Throws<FileNotFoundException>(() => loader.Load(_tempPath));
    }

    [Fact]
    public void Load_parses_file_without_frontmatter()
    {
        File.WriteAllText(_tempPath, """
# SYSTEM
Sys content.

# USER
User content.

# RETRY_FEEDBACK
Retry content.
""");
        var loader = new FilePromptLoader();

        var sections = loader.Load(_tempPath);

        Assert.Equal("Sys content.", sections.System.Trim());
        Assert.Equal("User content.", sections.User.Trim());
        Assert.Equal("Retry content.", sections.RetryFeedback.Trim());
    }

    [Fact]
    public void Load_returns_empty_section_when_section_missing()
    {
        File.WriteAllText(_tempPath, """
# SYSTEM
Only system here.
""");
        var loader = new FilePromptLoader();

        var sections = loader.Load(_tempPath);

        Assert.Equal("Only system here.", sections.System.Trim());
        Assert.Equal(string.Empty, sections.User.Trim());
        Assert.Equal(string.Empty, sections.RetryFeedback.Trim());
    }

    [Fact]
    public void Load_parses_three_sections()
    {
        File.WriteAllText(_tempPath, """
---
version: 1
language: fr
---

# SYSTEM
You are a system.

# USER
Hello {{name}}.

# RETRY_FEEDBACK
Previous attempt failed.
""");

        var loader = new FilePromptLoader();

        var sections = loader.Load(_tempPath);

        Assert.Equal("You are a system.", sections.System.Trim());
        Assert.Equal("Hello {{name}}.", sections.User.Trim());
        Assert.Equal("Previous attempt failed.", sections.RetryFeedback.Trim());
    }

    [Fact]
    public void Load_returns_updated_content_when_file_modified()
    {
        File.WriteAllText(_tempPath, """
# SYSTEM
Version 1.

# USER
.

# RETRY_FEEDBACK
.
""");
        var loader = new FilePromptLoader();

        var first = loader.Load(_tempPath);
        Assert.Equal("Version 1.", first.System.Trim());

        // Bump LastWriteTimeUtc explicitly — File.WriteAllText alone may keep the same
        // timestamp on systems with low-res clocks, which would mask the cache bug.
        File.WriteAllText(_tempPath, """
# SYSTEM
Version 2.

# USER
.

# RETRY_FEEDBACK
.
""");
        File.SetLastWriteTimeUtc(_tempPath, DateTime.UtcNow.AddSeconds(1));

        var second = loader.Load(_tempPath);
        Assert.Equal("Version 2.", second.System.Trim());
    }

    [Fact]
    public void Load_returns_cached_content_when_file_unchanged()
    {
        File.WriteAllText(_tempPath, """
# SYSTEM
Cached.

# USER
.

# RETRY_FEEDBACK
.
""");
        var fixedTime = DateTime.UtcNow.AddDays(-1);
        File.SetLastWriteTimeUtc(_tempPath, fixedTime);

        var loader = new FilePromptLoader();

        var first = loader.Load(_tempPath);

        // Overwrite the file content but RESTORE the original LastWriteTimeUtc — the loader
        // must trust the timestamp and serve from cache (this is the hot-reload contract).
        File.WriteAllText(_tempPath, """
# SYSTEM
Different but timestamp unchanged.

# USER
.

# RETRY_FEEDBACK
.
""");
        File.SetLastWriteTimeUtc(_tempPath, fixedTime);

        var second = loader.Load(_tempPath);

        Assert.Equal("Cached.", second.System.Trim());
        Assert.Same(first.System, second.System);  // identical reference: cache hit
    }
}
