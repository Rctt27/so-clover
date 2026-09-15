using System.Net;
using System.Text;
using SoClover.Eval.Langfuse;

namespace SoClover.Tests.Eval.Helpers;

/// <summary>Requête capturée AVANT que le client ne la libère : en-têtes et corps restent lisibles.</summary>
internal sealed record RecordedRequest(
    HttpMethod Method,
    string PathAndQuery,
    string? Authorization,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);

/// <summary>
/// Doublure de Langfuse : aucun test du harnais ne touche le réseau. Chaque requête est
/// consignée puis servie par la fonction fournie.
/// </summary>
internal sealed class LangfuseStubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _respond;

    public LangfuseStubHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> respond) =>
        _respond = respond;

    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!.PathAndQuery,
            request.Headers.Authorization?.ToString(),
            request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
            body));

        return _respond(request, body);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static LangfuseOptions Options() => new()
    {
        BaseUrl = "http://langfuse.test",
        PublicKey = "pk-lf-test",
        SecretKey = "sk-lf-test",
    };

    public static LangfuseClient Client(LangfuseStubHandler handler) =>
        new(new HttpClient(handler), Options());
}
