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
}
