using Xunit;

namespace SoClover.Tests.Eval.Tracing;

/// <summary>
/// Les écouteurs d'ActivitySource sont globaux au processus : deux TracerProvider actifs en
/// parallèle captureraient les spans l'un de l'autre. Toute suite qui démarre une session de
/// traçage vit dans cette collection, exécutée sans parallélisme.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TracingCollection
{
    public const string Name = "Tracing";
}
