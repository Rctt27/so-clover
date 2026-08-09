using System.Net;
using SoClover.Eval.Web;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Prouve que le FrameworkReference Microsoft.AspNetCore.App se propage bien de SoClover
/// (Microsoft.NET.Sdk.Web) vers SoClover.Eval (Microsoft.NET.Sdk) par la ProjectReference.
/// Tout le cycle P4-P5 repose dessus : sans ça, aucun serveur de séance n'existe.
/// Le WebApplication est construit PAR SoClover.Eval, jamais par le projet de test — qui
/// référence déjà Microsoft.AspNetCore.Mvc.Testing et masquerait l'échec.
/// </summary>
public class WebApplicationSmokeTests
{
    [Fact]
    public async Task WebApplication_demarre_sur_la_boucle_locale_depuis_SoClover_Eval()
    {
        var app = HumanServer.BuildSmokeApp(port: 0);
        await app.StartAsync();
        try
        {
            var baseUrl = HumanServer.ResolveUrl(app);
            Assert.StartsWith($"http://{HumanServer.LoopbackHost}:", baseUrl, StringComparison.Ordinal);

            using var client = new HttpClient();
            var response = await client.GetAsync(new Uri(new Uri(baseUrl), "/api/ping"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"ok\":true", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
