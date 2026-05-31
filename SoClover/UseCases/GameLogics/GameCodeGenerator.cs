using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SoClover.Domain;

namespace SoClover.UseCases.GameLogics;

public sealed class GameCodeGenerator : IGameCodeGenerator
{
    private const string CodeLanguage = "English";
    private const int WordCount = 4;

    private readonly IWordDictionary _dictionary;

    public GameCodeGenerator(IWordDictionary dictionary) => _dictionary = dictionary;

    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        var words = await _dictionary.GetRandomWordsAsync(CodeLanguage, WordCount, ct);
        return string.Join("-", words.Select(Slugify));
    }

    // Minuscules + suppression de tout caractère non alphanumérique
    // (neutralise les entrées multi-mots du dictionnaire : "Water Bottle" -> "waterbottle").
    private static string Slugify(string word)
    {
        var chars = word.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
        return new string(chars);
    }
}
