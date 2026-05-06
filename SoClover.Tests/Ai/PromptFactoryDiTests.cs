using Microsoft.Extensions.DependencyInjection;
using SoClover.Infrastructure.AI.Prompts;
using Xunit;

namespace SoClover.Tests.AI;

public sealed class PromptFactoryDiTests
{
    [Fact]
    public void Factory_resolves_via_DI_as_singleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAiCluePromptProviderFactory, AiCluePromptProviderFactory>();
        using var provider = services.BuildServiceProvider();

        var factory1 = provider.GetRequiredService<IAiCluePromptProviderFactory>();
        var factory2 = provider.GetRequiredService<IAiCluePromptProviderFactory>();

        Assert.NotNull(factory1);
        Assert.Same(factory1, factory2);
    }
}
