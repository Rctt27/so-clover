using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SoClover.Eval.Tracing;

/// <summary>
/// Durée de vie du traçage d'un verbe. Processeur <b>synchrone</b> : chaque span part à sa
/// fermeture, un échec est donc connu au plus tard au <see cref="Checkpoint"/> de son unité —
/// qui le lève <b>avant</b> que le verbe n'écrive les lignes de l'unité (spec §5).
/// </summary>
public sealed class TracingSession : IDisposable
{
    private const int FlushTimeoutMs = 10_000;
    private readonly TracerProvider? _provider;
    private readonly FailFastExporter? _exporter;

    private TracingSession(TracingManifest manifest, TracerProvider? provider, FailFastExporter? exporter)
    {
        Manifest = manifest;
        _provider = provider;
        _exporter = exporter;
    }

    public TracingManifest Manifest { get; }

    public bool IsOn => _provider is not null;

    public static TracingSession Off() => new(TracingManifest.Off, null, null);

    public static TracingSession Start(BaseExporter<Activity> exporter, TracingManifest manifest)
    {
        var failFast = new FailFastExporter(exporter);
        var provider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("soclover-eval"))
            .SetSampler(new AlwaysOnSampler())
            .AddSource(EvalTracing.SourceName, EvalTracing.ChatSourceName)
            .AddProcessor(new SimpleActivityExportProcessor(failFast))
            .Build();
        return new TracingSession(manifest, provider, failFast);
    }

    public void Checkpoint(string unit)
    {
        if (_provider is null)
            return;

        if (!_provider.ForceFlush(FlushTimeoutMs))
            throw new TracingLostException(unit, $"flush non terminé en {FlushTimeoutMs} ms");
        if (_exporter!.TakeFailure() is { } failure)
            throw new TracingLostException(unit, failure);
    }

    public void Dispose() => _provider?.Dispose();
}
