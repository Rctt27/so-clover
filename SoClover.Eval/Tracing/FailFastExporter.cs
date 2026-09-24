using System.Diagnostics;
using OpenTelemetry;

namespace SoClover.Eval.Tracing;

/// <summary>
/// Enveloppe d'exporteur qui <b>retient</b> les échecs : le SDK les avale (journal interne), or la
/// politique de la phase 3 (P2) veut qu'une perte de télémétrie arrête le run. Une exception de
/// l'exporteur enveloppé compte comme un échec, jamais comme un crash du processeur.
/// </summary>
public sealed class FailFastExporter(BaseExporter<Activity> inner) : BaseExporter<Activity>
{
    private string? _failure;

    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            var result = inner.Export(batch);
            if (result != ExportResult.Success)
                _failure ??= $"export refusé ({batch.Count} span(s))";
            return result;
        }
        catch (Exception ex)
        {
            _failure ??= $"export en erreur : {ex.Message}";
            return ExportResult.Failure;
        }
    }

    /// <summary>Premier échec depuis le dernier appel, puis remise à zéro.</summary>
    public string? TakeFailure()
    {
        var failure = _failure;
        _failure = null;
        return failure;
    }

    protected override bool OnForceFlush(int timeoutMilliseconds) => inner.ForceFlush(timeoutMilliseconds);

    protected override bool OnShutdown(int timeoutMilliseconds) => inner.Shutdown(timeoutMilliseconds);

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
