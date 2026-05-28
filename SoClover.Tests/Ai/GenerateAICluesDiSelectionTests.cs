using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoClover.Infrastructure.AI;
using SoClover.UseCases.AI;
using Xunit;

namespace SoClover.Tests.AI;

public class GenerateAICluesDiSelectionTests
{
    [Fact]
    public void Default_GenerationMode_resolves_PerBoard_Handler()
    {
        var sp = BuildProviderForMode(generationMode: null);

        var resolved = sp.GetRequiredService<IGenerateAICluesUseCase>();

        Assert.IsType<GenerateAIClues.Handler>(resolved);
    }

    [Fact]
    public void PerBoard_GenerationMode_resolves_PerBoard_Handler()
    {
        var sp = BuildProviderForMode(AiClueGenerationMode.PerBoard);

        var resolved = sp.GetRequiredService<IGenerateAICluesUseCase>();

        Assert.IsType<GenerateAIClues.Handler>(resolved);
    }

    [Fact]
    public void PerDirection_GenerationMode_resolves_PerDirection_Handler()
    {
        var sp = BuildProviderForMode(AiClueGenerationMode.PerDirection);

        var resolved = sp.GetRequiredService<IGenerateAICluesUseCase>();

        Assert.IsType<GenerateAICluesPerDirection.Handler>(resolved);
    }

    private static ServiceProvider BuildProviderForMode(AiClueGenerationMode? generationMode)
    {
        var services = new ServiceCollection();
        var options = new LlmOptions();
        if (generationMode is { } mode) options.GenerationMode = mode;
        services.AddSingleton(Options.Create(options));

        DiSelectionTestStubs.Register(services);

        services.AddTransient<IGenerateAICluesUseCase>(sp =>
        {
            var mode = sp.GetRequiredService<IOptions<LlmOptions>>().Value.GenerationMode;
            return mode == AiClueGenerationMode.PerDirection
                ? ActivatorUtilities.CreateInstance<GenerateAICluesPerDirection.Handler>(sp)
                : ActivatorUtilities.CreateInstance<GenerateAIClues.Handler>(sp);
        });

        return services.BuildServiceProvider();
    }
}
