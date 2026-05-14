using System.Threading.Channels;
using SoClover.UseCases.AI;

namespace SoClover.Infrastructure.AI;

/// <summary>
/// Singleton wrapper around a bounded channel of AI clue generation requests.
/// Produced by StartWritingPhase (one message per AI player), consumed by
/// AiClueOrchestratorHostedService. Bounded (100) with FullMode=DropWrite so
/// StartWritingPhase.Handle() never blocks when the consumer falls behind.
/// Drops are logged as warnings by the producer.
/// </summary>
public sealed class AiClueWorkChannel
{
    private readonly Channel<AiClueGenerationRequested> _channel;

    public AiClueWorkChannel()
    {
        _channel = Channel.CreateBounded<AiClueGenerationRequested>(
            new BoundedChannelOptions(capacity: 100)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
            });
    }

    public ChannelWriter<AiClueGenerationRequested> Writer => _channel.Writer;
    public ChannelReader<AiClueGenerationRequested> Reader => _channel.Reader;
}
