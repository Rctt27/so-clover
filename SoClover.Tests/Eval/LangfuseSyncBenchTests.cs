using System.Net;
using SoClover.Eval.Langfuse;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Teste <see cref="LangfuseSyncCommand"/> côté banc, sans réseau ni fichier : la surcharge
/// <c>internal</c> qui prend un <c>BenchContents</c> déjà lu isole la logique de synchronisation
/// de la lecture disque (<c>BenchFile.Read</c>), pour que ces trois scénarios se testent avec
/// <see cref="LangfuseStubHandler"/>.
/// </summary>
public class LangfuseSyncBenchTests
{
    [Fact]
    public async Task Un_dataset_existant_a_un_autre_benchHash_est_refuse_sans_aucun_post()
    {
        var handler = new LangfuseStubHandler((_, _) => LangfuseStubHandler.Json(
            """{"id":"ds_1","name":"soclover-bench-dev","metadata":{"benchHash":"autrehash"}}"""));
        var bench = LangfuseFixtures.Bench();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LangfuseSyncCommand.SyncBenchAsync(
                LangfuseStubHandler.Client(handler), bench, "eval/boards.dev.jsonl", CancellationToken.None));

        Assert.Contains("soclover-bench-dev", ex.Message);
        // Aucun POST envoyé : le refus intervient AVANT tout appel réseau d'écriture.
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
    }

    [Fact]
    public async Task Un_dataset_absent_est_cree_puis_les_items_sont_postes_un_par_un()
    {
        var handler = new LangfuseStubHandler((request, _) =>
            request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : request.RequestUri!.AbsolutePath == "/api/public/v2/datasets"
                    ? LangfuseStubHandler.Json(
                        """{"id":"ds_1","name":"soclover-bench-dev","metadata":{"benchHash":"416b819a41a1"}}""")
                    : LangfuseStubHandler.Json("{}"));

        var bench = LangfuseFixtures.Bench();
        await LangfuseSyncCommand.SyncBenchAsync(
            LangfuseStubHandler.Client(handler), bench, "eval/boards.dev.jsonl", CancellationToken.None);

        // 1 GET (absent) + 1 POST création + 4 POST items (fixture : 1 board x 4 directions).
        Assert.Equal(6, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("/api/public/v2/datasets", handler.Requests[1].PathAndQuery);
        Assert.Equal(
            4, handler.Requests.Skip(2).Count(r => r.PathAndQuery == "/api/public/dataset-items"));
    }

    [Fact]
    public async Task Un_item_dont_le_post_echoue_est_nomme_dans_l_exception()
    {
        var handler = new LangfuseStubHandler((request, body) =>
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (request.RequestUri!.AbsolutePath == "/api/public/v2/datasets")
                return LangfuseStubHandler.Json(
                    """{"id":"ds_1","name":"soclover-bench-dev","metadata":{"benchHash":"416b819a41a1"}}""");
            if (body!.Contains("dev-001-Right"))
                return LangfuseStubHandler.Json("""{"message":"boom"}""", HttpStatusCode.InternalServerError);
            return LangfuseStubHandler.Json("{}");
        });

        var bench = LangfuseFixtures.Bench();
        var ex = await Assert.ThrowsAsync<LangfuseException>(() =>
            LangfuseSyncCommand.SyncBenchAsync(
                LangfuseStubHandler.Client(handler), bench, "eval/boards.dev.jsonl", CancellationToken.None));

        Assert.Contains("dev-001-Right", ex.Message);
    }
}
