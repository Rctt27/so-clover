using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using SoClover.Eval.Tracing;

namespace SoClover.Tests.Eval.Helpers;

internal static class TracingTestKit
{
    public static TracingSession Start(out List<Activity> exported)
    {
        exported = [];
        return TracingSession.Start(new InMemoryExporter<Activity>(exported), TracingManifest.Langfuse("http://langfuse.test"));
    }

    public static TracingSession StartFailing() =>
        TracingSession.Start(new FailingExporter(), TracingManifest.Langfuse("http://langfuse.test"));

    public static Activity Single(IEnumerable<Activity> exported, string name) =>
        exported.Single(a => a.DisplayName == name);

    public static string? Tag(Activity activity, string key) =>
        activity.TagObjects.FirstOrDefault(t => t.Key == key).Value?.ToString();

    private sealed class FailingExporter : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch) => ExportResult.Failure;
    }
}
