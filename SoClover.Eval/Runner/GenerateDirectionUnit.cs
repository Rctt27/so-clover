using System.Diagnostics;
using System.Text.Json.Nodes;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Io;
using SoClover.Eval.Langfuse;
using SoClover.Eval.Tracing;

namespace SoClover.Eval.Runner;

/// <summary>
/// Une direction de <c>generate</c> — l'unité atomique du traçage (spec phase 3, §5). Les tentatives
/// sont rendues, jamais écrites : c'est à l'appelant de les consigner APRÈS
/// <see cref="TracingSession.Checkpoint"/>. Chaque tentative ouvre son span avant l'appel LLM qu'elle
/// provoque (le span <c>chat</c> en devient l'enfant, par <see cref="Activity.Current"/>).
/// </summary>
public static class GenerateDirectionUnit
{
    public static async Task<IReadOnlyList<RunAttempt>> RunAsync(
        ClueRunner runner, BenchBoard board, Direction direction, int maxAttempts, string runId, CancellationToken ct)
    {
        var itemId = BenchDatasetMapper.ItemId(board.BoardId, direction.ToString());
        var attempts = new List<RunAttempt>();

        using var root = EvalTracing.StartRoot("generate-direction", OtlpIds.TraceId(runId, itemId));
        root?.SetTag(EvalTracing.SessionId, runId);
        root?.SetTag(EvalTracing.Environment, "generate");
        root?.SetTag(EvalTracing.Input, BenchDatasetMapper.Input(board, direction.ToString()).ToJsonString(EvalJson.Options));

        await using (var enumerator = runner.RunDirectionAsync(board, direction, ct).GetAsyncEnumerator(ct))
        {
            // ClueRunner s'arrête sur la première tentative valide ou après maxAttempts : on s'arrête
            // au même point, sans rappeler MoveNextAsync (qui ouvrirait un span vide).
            for (var n = 0; n < maxAttempts; n++)
            {
                using var span = EvalTracing.Source.StartActivity($"generate attempt {n}");
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    break;

                var attempt = enumerator.Current;
                attempts.Add(attempt);
                span?.SetTag(EvalTracing.SessionId, runId);
                span?.SetTag(EvalTracing.Output, attempt.Clue ?? $"échec : {attempt.FailureKind}");
                span?.SetTag(EvalTracing.Metadata("valid"), attempt.Valid ? "true" : "false");
                span?.SetTag(EvalTracing.Metadata("rejection_rules"), string.Join(",", attempt.RejectionRules));
                if (attempt.FailureKind is not null)
                    span?.SetTag(EvalTracing.Metadata("failure_kind"), attempt.FailureKind);

                if (attempt.Valid)
                    break;
            }
        }

        var retained = attempts.FirstOrDefault(a => a.Valid);
        root?.SetTag(EvalTracing.Output, new JsonObject
        {
            ["clue"] = retained?.Clue,
            ["valid"] = retained is not null,
            ["attempts"] = attempts.Count,
        }.ToJsonString(EvalJson.Options));
        return attempts;
    }
}
