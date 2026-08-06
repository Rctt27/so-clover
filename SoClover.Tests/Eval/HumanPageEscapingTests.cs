using System.Text.RegularExpressions;
using Xunit;

namespace SoClover.Tests.Eval;

/// <summary>
/// Les deux pages des séances humaines assemblent leur DOM par concaténation de chaînes, puis
/// l'injectent via <c>innerHTML</c>. Le seul rempart contre l'injection est la fonction
/// <c>esc()</c> — et son mode de défaillance n'est pas l'oubli, c'est le <b>débordement</b> :
/// envelopper dans <c>esc()</c> une expression qui contient déjà du balisage voulu.
/// <para>
/// Régression constatée en séance B : <c>esc(view.referenceWords.join("&lt;/span&gt; + &lt;span
/// class=\"pair\"&gt;"))</c> échappait le séparateur qu'il venait d'assembler, et le juge lisait
/// « … mène le plus directement à Marron&lt;/span&gt; + &lt;span class="pair"&gt;Collier ? ». Rien
/// de dangereux, mais la question du PRD doit s'afficher <b>littéralement</b> : c'est elle qui
/// ancre le jugement, et un juge qui déchiffre des balises ne juge plus dans les mêmes conditions
/// d'un item à l'autre.
/// </para>
/// <para>
/// L'invariant testé est plus large que ce cas : <b>aucun argument de <c>esc()</c> ne contient de
/// balisage</b>. Échapper mot à mot puis joindre avec le balisage — jamais l'inverse.
/// </para>
/// </summary>
public class HumanPageEscapingTests
{
    /// <summary>
    /// <c>esc(</c> suivi de balisage avant la parenthèse fermante. <c>[^)]*</c> s'arrête à la
    /// première parenthèse fermante, donc <c>esc(mot)</c> et <c>esc(w)</c> ne matchent jamais,
    /// tandis qu'un <c>esc(…join("&lt;/span&gt;…</c> matche.
    /// </summary>
    private static readonly Regex MarkupInsideEsc = new(@"esc\([^)]*<", RegexOptions.Compiled);

    public static TheoryData<string> Pages() => new() { "judge.html", "elicit.html" };

    [Theory]
    [MemberData(nameof(Pages))]
    public void Aucun_balisage_ne_transite_par_esc(string page)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Web", "Pages", page);
        Assert.True(File.Exists(path), $"Page absente de la sortie de build : {path}");

        var offending = File.ReadAllLines(path)
            .Select((line, index) => (Text: line.Trim(), Number: index + 1))
            .Where(l => MarkupInsideEsc.IsMatch(l.Text))
            .ToList();

        Assert.True(offending.Count == 0,
            $"{page} : du balisage transite par esc() et s'affichera littéralement à l'écran. " +
            "Échapper chaque valeur (.map((w) => esc(w))) PUIS joindre avec le balisage, " +
            "jamais joindre d'abord." + Environment.NewLine +
            string.Join(Environment.NewLine, offending.Select(l => $"  ligne {l.Number} : {l.Text}")));
    }
}
