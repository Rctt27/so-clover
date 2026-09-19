using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace SoClover.Eval.Tracing;

/// <summary>
/// Repli d'O1 : M.E.AI 9.5 ne place pas les messages là où Langfuse les lit. Placé SOUS
/// l'instrumentation, ce client voit le span <c>chat</c> comme <see cref="Activity.Current"/> et y
/// pose entrée et sortie sous les attributs que Langfuse interprète.
/// </summary>
internal sealed class LangfuseIoChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    /// <summary>
    /// <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> : sans lui, l'encodeur par défaut
    /// échappe les caractères non-ASCII (les accents français des prompts et réponses) en
    /// <c>\uXXXX</c>, illisible dans l'UI Langfuse. Le contenu ici, ce sont des mots de banc, pas des
    /// données sensibles — le compromis sécurité de cet encodeur ne s'applique pas.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        Activity.Current?.SetTag(EvalTracing.Input,
            JsonSerializer.Serialize(list.Select(m => new { role = m.Role.Value, content = m.Text }), SerializerOptions));
        var response = await base.GetResponseAsync(list, options, cancellationToken).ConfigureAwait(false);
        Activity.Current?.SetTag(EvalTracing.Output, response.Text);
        return response;
    }
}
