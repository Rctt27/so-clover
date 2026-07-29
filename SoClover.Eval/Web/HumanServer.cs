using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SoClover.Eval.Human;

namespace SoClover.Eval.Web;

/// <summary>Corps de <c>POST /api/attempt</c>.</summary>
public sealed record AttemptRequest(string? Clue, string Outcome, string? RelationType, int ElapsedSeconds);

/// <summary>Corps de <c>POST /api/assisted</c>. Les deux champs sont facultatifs.</summary>
public sealed record AssistedRequest(string? AssistedClue, string? Notes);

/// <summary>Corps de <c>POST /api/verdict</c> et <c>POST /api/rejudge</c>.</summary>
public sealed record VerdictRequest(string PositionChoice, long ElapsedMs);

/// <summary>
/// Serveur des séances humaines. Écoute exclusivement sur la boucle locale : l'outil est un
/// instrument de mesure personnel, jamais un service. C'est le serveur — et non la page — qui
/// applique le protocole (verrou A-4 et chrono serveur pour la séance A, aveuglement structurel et
/// séquencement pour la séance B, quotas et écriture append-only pour les deux) : une page
/// statique ne pourrait garantir aucun des principes du PRD.
/// <para>
/// Nuance entre les deux séances : la séance A recoupe le chrono client avec un
/// <c>ServerElapsedSeconds</c> mesuré entre <c>/api/next</c> et <c>/api/attempt</c>
/// (<see cref="ElicitationLine"/>). La séance B n'a <b>aucun</b> pendant serveur à
/// <see cref="ComparisonLine.ElapsedMs"/> — cette valeur reste purement déclarative, fournie par
/// le client. Ce que <see cref="JudgeSession"/> garantit côté serveur pour la séance B, ce n'est
/// pas la véracité du chrono, mais le <b>séquencement</b> : <c>SubmitVerdict</c>/
/// <c>ReJudgeLast</c> refusent toute consignation tant que <c>Next()</c> n'a pas réellement servi
/// l'item courant (ce qui, au passage, empêche aussi de sauter une pause de quota due).
/// </para>
/// </summary>
public static class HumanServer
{
    public const string LoopbackHost = "127.0.0.1";

    /// <summary>
    /// Application minimale servant uniquement à prouver la propagation du FrameworkReference
    /// Microsoft.AspNetCore.App vers SoClover.Eval. Conservée : c'est le canari qui préviendra
    /// si une future modification du csproj rompt cette propagation.
    /// </summary>
    public static WebApplication BuildSmokeApp(int port)
    {
        var app = NewApp(port);
        app.MapGet("/api/ping", () => Results.Json(new { ok = true }));
        return app;
    }

    public static WebApplication BuildElicitApp(ElicitationSession session, string pagePath, int port)
    {
        var app = NewApp(port);

        app.MapGet("/", () => Page(pagePath));
        app.MapGet("/api/next", () => Results.Json(session.Next()));

        app.MapPost("/api/attempt", (AttemptRequest request) => Map(session.SubmitAttempt(
            request.Clue, request.Outcome, request.RelationType, request.ElapsedSeconds)));

        app.MapGet("/api/candidates", () =>
        {
            var view = session.Candidates();
            return view.Status switch
            {
                SessionStatus.Ok => Results.Json(view),
                SessionStatus.Conflict => Results.Json(new { message = view.Message }, statusCode: 409),
                _ => Results.Json(new { message = view.Message }, statusCode: 409),
            };
        });

        app.MapPost("/api/assisted", (AssistedRequest request) =>
            Map(session.RecordAssist(request.AssistedClue, request.Notes)));

        return app;
    }

    public static WebApplication BuildJudgeApp(JudgeSession session, string pagePath, int port)
    {
        var app = NewApp(port);

        app.MapGet("/", () => Page(pagePath));
        app.MapGet("/api/next", () => Results.Json(session.Next()));

        app.MapPost("/api/verdict", (VerdictRequest request) =>
            Map(session.SubmitVerdict(request.PositionChoice, request.ElapsedMs)));

        app.MapPost("/api/rejudge", (VerdictRequest request) =>
            Map(session.ReJudgeLast(request.PositionChoice, request.ElapsedMs)));

        return app;
    }

    /// <summary>URL effective. Avec <c>port = 0</c>, elle n'est connue qu'après <c>StartAsync</c>.</summary>
    public static string ResolveUrl(WebApplication app) =>
        app.Urls.FirstOrDefault()
        ?? throw new InvalidOperationException("Aucune URL liée : l'application n'a pas encore démarré.");

    internal static WebApplication NewApp(int port)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://{LoopbackHost}:{port}");
        return builder.Build();
    }

    internal static IResult Page(string pagePath) =>
        File.Exists(pagePath)
            ? Results.Content(File.ReadAllText(pagePath), "text/html; charset=utf-8")
            : Results.Problem($"Page introuvable : {pagePath}", statusCode: 500);

    /// <summary>
    /// Traduction unique <c>SessionStatus</c> → HTTP. Les codes sont du protocole, pas du confort :
    /// <c>409</c> est le verrou A-4, <c>422</c> le refus par les règles du jeu (avec sa règle),
    /// <c>400</c> une valeur hors vocabulaire fermé.
    /// </summary>
    internal static IResult Map(SessionResult result) => result.Status switch
    {
        SessionStatus.Ok => Results.Json(new { ok = true }),
        SessionStatus.Rejected => Results.Json(
            new { message = result.Message, rejectionRules = result.RejectionRules }, statusCode: 422),
        SessionStatus.Conflict => Results.Json(new { message = result.Message }, statusCode: 409),
        SessionStatus.BadRequest => Results.Json(new { message = result.Message }, statusCode: 400),
        SessionStatus.Finished => Results.Json(new { message = result.Message, finished = true }, statusCode: 409),
        _ => Results.StatusCode(500),
    };
}
