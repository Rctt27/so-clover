using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SoClover.Eval.Web;

/// <summary>
/// Serveur des séances humaines. Écoute exclusivement sur la boucle locale : l'outil est un
/// instrument de mesure personnel, jamais un service. C'est le serveur — et non la page — qui
/// applique le protocole (verrou A-4, aveuglement, quotas, chrono, écriture append-only) :
/// une page statique ne pourrait garantir aucun des six principes du PRD.
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
}
