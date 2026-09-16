using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using System.Web;

namespace SoClover.Tests.Eval.Helpers;

/// <summary>Sert les trois endpoints de prompts que le harnais consomme, pour un seul nom de prompt.</summary>
internal static class FakeLangfusePrompts
{
    public static LangfuseStubHandler Serve(string name, params (int Version, string Content, string[] Labels)[] versions) =>
        new((request, _) =>
        {
            var uri = request.RequestUri!;
            var query = HttpUtility.ParseQueryString(uri.Query);

            if (uri.AbsolutePath == "/api/public/v2/prompts" && query["name"] == name)
            {
                return LangfuseStubHandler.Json(new JsonObject
                {
                    ["data"] = new JsonArray(new JsonObject
                    {
                        ["name"] = name,
                        ["versions"] = new JsonArray(versions.Select(v => (JsonNode?)v.Version).ToArray()),
                    }),
                    ["meta"] = new JsonObject(),
                }.ToJsonString());
            }

            if (uri.AbsolutePath == $"/api/public/v2/prompts/{name}")
            {
                var hit = query["version"] is { } v
                    ? versions.FirstOrDefault(x => x.Version == int.Parse(v, CultureInfo.InvariantCulture))
                    : versions.LastOrDefault(x => x.Labels.Contains(query["label"]));
                if (hit.Content is not null)
                    return LangfuseStubHandler.Json(PromptJson(name, hit).ToJsonString());
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

    public static JsonObject PromptJson(string name, (int Version, string Content, string[] Labels) v) => new()
    {
        ["name"] = name,
        ["type"] = "text",
        ["version"] = v.Version,
        ["prompt"] = v.Content,
        ["labels"] = new JsonArray(v.Labels.Select(l => (JsonNode?)l).ToArray()),
    };
}
