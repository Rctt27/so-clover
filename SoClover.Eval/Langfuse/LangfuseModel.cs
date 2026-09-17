using System.Text.Json.Nodes;

namespace SoClover.Eval.Langfuse;

/// <summary>Une version d'un prompt de type <c>text</c>. <see cref="Version"/> est la numérotation de Langfuse, pas le <c>version:</c> du frontmatter.</summary>
public sealed record LangfusePrompt(string Name, int Version, string Content, IReadOnlyList<string> Labels);

public sealed record LangfuseDataset(string Id, string Name, string? BenchHash);

public sealed record LangfuseDatasetItem(string Id, JsonObject Input, JsonObject ExpectedOutput, JsonObject Metadata);

/// <summary>Un score NUMERIC. Exactement une cible : (<see cref="TraceId"/>, <see cref="ObservationId"/>) ou <see cref="DatasetRunId"/>.</summary>
public sealed record LangfuseScore(
    string Id,
    string Name,
    double Value,
    string? TraceId,
    string? ObservationId,
    string? DatasetRunId,
    string? Comment);

public sealed class LangfuseException(string message, Exception? inner = null) : Exception(message, inner);
