using System.Globalization;
using System.Text;

namespace SoClover.Domain;

public static class TextNormalizer
{
    public static string Normalize(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // 1) Map ligatures & typographic apostrophes BEFORE FormD stripping
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input.Trim().ToLowerInvariant())
        {
            switch (ch)
            {
                case 'œ': sb.Append("oe"); break;
                case 'æ': sb.Append("ae"); break;
                case '\u2019': // right single quotation mark
                case '\u2018': // left single quotation mark
                    sb.Append('\''); break;
                default: sb.Append(ch); break;
            }
        }

        // 2) Strip diacritics via FormD
        var decomposed = sb.ToString().Normalize(NormalizationForm.FormD);
        var stripped = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                stripped.Append(c);
        }

        return stripped.ToString().Normalize(NormalizationForm.FormC);
    }
}