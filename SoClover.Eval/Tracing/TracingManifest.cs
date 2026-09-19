using System.Text.Json.Serialization;

namespace SoClover.Eval.Tracing;

/// <summary>
/// Champ <c>tracing</c> des manifestes (spec phase 3, §5). Hors hash8 et hors empreinte de
/// décodeur : tracer ne change pas l'identité d'une mesure. <c>null</c> = manifeste antérieur à la
/// phase 3, relu inchangé.
/// </summary>
public sealed record TracingManifest(
    string Target,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Host = null)
{
    public const string LangfuseTarget = "langfuse";
    public const string OffTarget = "off";

    public static TracingManifest Off { get; } = new(OffTarget);

    public static TracingManifest Langfuse(string host) => new(LangfuseTarget, host);

    [JsonIgnore]
    public bool IsOn => Target == LangfuseTarget;

    /// <summary>
    /// Une reprise se fait sous le mode de traçage d'origine (spec §8 bis) : sinon l'experiment
    /// omettrait les unités de la session non tracée. Un manifeste historique vaut <c>off</c>.
    /// </summary>
    public static void RequireSameMode(TracingManifest? existing, TracingManifest current, string path)
    {
        var existingTarget = existing?.Target ?? OffTarget;
        if (existingTarget == current.Target)
            return;

        throw new InvalidOperationException(
            $"{path} a été produit avec le traçage « {existingTarget} », la commande courante trace en " +
            $"« {current.Target} ». Reprendre mélangerait des unités tracées et non tracées. " +
            (existingTarget == OffTarget
                ? "Reprendre avec --trace off, ou --force pour repartir de zéro."
                : "Reprendre sans --trace off (Langfuse démarré), ou --force pour repartir de zéro."));
    }
}
