using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using SoClover.Eval.Langfuse;

namespace SoClover.Eval.Tracing;

/// <summary>
/// Exporteur OTLP/HTTP protobuf vers Langfuse. Authentification par un HttpClient fourni (la chaîne
/// <c>Headers</c> découpe sur <c>=</c>, que le base64 contient) ; en-tête d'ingestion v4, sans lequel
/// les spans mettent jusqu'à dix minutes à apparaître (même en-tête que
/// <see cref="LangfuseClient.SendOtlpTracesAsync"/>).
/// </summary>
public static class LangfuseOtlp
{
    public static BaseExporter<Activity> CreateExporter(LangfuseOptions options) =>
        new OtlpTraceExporter(new OtlpExporterOptions
        {
            Endpoint = new Uri(options.BaseUrl.TrimEnd('/') + "/api/public/otel/v1/traces"),
            Protocol = OtlpExportProtocol.HttpProtobuf,
            HttpClientFactory = () =>
            {
                var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.PublicKey}:{options.SecretKey}")));
                http.DefaultRequestHeaders.Add("x-langfuse-ingestion-version", "4");
                return http;
            },
        });
}
