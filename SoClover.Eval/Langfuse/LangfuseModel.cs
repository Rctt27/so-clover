namespace SoClover.Eval.Langfuse;

/// <summary>Une version d'un prompt de type <c>text</c>. <see cref="Version"/> est la numérotation de Langfuse, pas le <c>version:</c> du frontmatter.</summary>
public sealed record LangfusePrompt(string Name, int Version, string Content, IReadOnlyList<string> Labels);

public sealed class LangfuseException(string message, Exception? inner = null) : Exception(message, inner);
