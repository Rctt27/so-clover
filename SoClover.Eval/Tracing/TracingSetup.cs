using SoClover.Eval.Bench;
using SoClover.Eval.Cli;
using SoClover.Eval.Langfuse;

namespace SoClover.Eval.Tracing;

/// <summary>
/// Option <c>--trace langfuse|off</c> (défaut : langfuse) de <c>generate</c>, <c>decode</c> et
/// <c>calibrate</c>. Aucun repli : clés absentes, Langfuse injoignable, banc de test ou pseudo-run
/// humain ⇒ refus avant tout appel LLM, en nommant <c>--trace off</c>.
/// </summary>
public static class TracingSetup
{
    public static bool WantsTracing(Args args, BenchManifest bench, bool isHumanRun)
    {
        var mode = args.Get("trace") ?? TracingManifest.LangfuseTarget;
        if (mode == TracingManifest.OffTarget)
            return false;
        if (mode != TracingManifest.LangfuseTarget)
            throw new InvalidOperationException($"--trace {mode} inconnu : langfuse (défaut) ou off.");

        if (BenchDatasetMapper.IsTestBench(bench))
            throw new InvalidOperationException(
                "Le banc de test ne se trace pas dans Langfuse (§7.1 du PRD) : relancer avec --trace off.");
        if (isHumanRun)
            throw new InvalidOperationException(
                "Un pseudo-run humain ne devient pas une experiment Langfuse (D5) : relancer avec --trace off.");
        return true;
    }

    public static async Task<TracingSession> StartAsync(
        Args args, LangfuseOptions options, BenchManifest bench, bool isHumanRun, CancellationToken ct)
    {
        if (!WantsTracing(args, bench, isHumanRun))
            return TracingSession.Off();

        var client = LangfuseClientFactory.CreateOrNull(options) ?? throw new InvalidOperationException(
            "Le traçage Langfuse est actif par défaut mais les clés sont absentes (LANGFUSE__PUBLICKEY / " +
            "LANGFUSE__SECRETKEY ou SoClover.Eval/evalsettings.local.json) : les renseigner, ou --trace off.");
        await TracePreflight.RunAsync(client, ct).ConfigureAwait(false);
        return TracingSession.Start(LangfuseOtlp.CreateExporter(options), TracingManifest.Langfuse(options.BaseUrl));
    }
}
