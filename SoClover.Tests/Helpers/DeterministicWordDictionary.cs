using SoClover.Domain;

namespace SoClover.Tests.Helpers;

/// <summary>
/// Dictionnaire à vocabulaire synthétique pour les suites qui ne testent pas le contenu des vrais
/// dictionnaires. Il supprime la source d'intermittence de la porte de non-régression : les mots de
/// carte sont tirés aléatoirement (<c>WordsPool.DrawWords</c> instancie un <c>Random</c> non seedé à
/// chaque tirage), et plusieurs tests reposent implicitement sur des propriétés que le vrai
/// dictionnaire ne garantit pas pour tous les tirages :
/// <list type="number">
/// <item>« un mot du board utilisé comme indice est rejeté » — c'était faux pour « Nu », « Os »,
/// « Or » (FR), sous le seuil de visibilité du validateur ; le seuil a depuis été abaissé côté
/// produit, l'invariant est ici conservé comme marge (mots d'un seul caractère, futurs seuils) ;</item>
/// <item>« un indice littéral de test n'entre en conflit avec aucun mot de carte » — faux pour
/// « Botte » (FR), dont la racine R2 « bott » est une sous-chaîne de « admin-bottom ».</item>
/// </list>
/// Le parti pris n'est pas de figer le tirage avec un seed — un tirage figé peut être précisément le
/// tirage fatal — mais de rendre tous les tirages équivalents : chaque mot est assez long pour être vu
/// du validateur et contient un chiffre, ce qui le met hors d'atteinte de tout indice fait de lettres.
/// Les invariants sont verrouillés par <see cref="DeterministicWordDictionaryTests"/>.
/// </summary>
public sealed class DeterministicWordDictionary : IWordDictionary
{
    /// <summary>Confortablement au-dessus des 64 mots consommés par une partie à 4 joueurs.</summary>
    private const int WordCount = 512;

    private static readonly IReadOnlyList<string> Words =
        Enumerable.Range(0, WordCount).Select(i => $"Vhk{i:D3}").ToList();

    private readonly Random _random;

    /// <param name="seed">
    /// Seed du tirage de <see cref="GetRandomWordsAsync"/> (codes de partie). Fixe par défaut : deux
    /// appels successifs donnent des tirages différents mais la séquence est reproductible d'un run à
    /// l'autre.
    /// </param>
    public DeterministicWordDictionary(int seed = 20260727) => _random = new Random(seed);

    public Task<IReadOnlyList<string>> GetAllWordsAsync(string language, CancellationToken ct = default)
        => Task.FromResult(Words);

    public Task<IReadOnlyList<string>> GetRandomWordsAsync(string language, int count, CancellationToken ct = default)
    {
        if (count > Words.Count)
            throw new InvalidOperationException(
                $"Deterministic dictionary has only {Words.Count} words, but {count} were requested");

        var available = Words.ToList();
        var drawn = new List<string>(count);
        lock (_random)
        {
            for (var i = 0; i < count; i++)
            {
                var index = _random.Next(available.Count);
                drawn.Add(available[index]);
                available.RemoveAt(index);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(drawn);
    }
}
