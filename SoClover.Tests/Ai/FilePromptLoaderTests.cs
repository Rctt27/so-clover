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
