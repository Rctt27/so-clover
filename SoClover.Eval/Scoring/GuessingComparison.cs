using SoClover.Eval.Decoder;
using SoClover.Eval.Human;

namespace SoClover.Eval.Scoring;

public sealed record GuessingComparisonResult(
    int PairedDirectionCount,
    double HumanRecovery,
    double DecoderRecovery,
    double Delta,
    double CiLow,
    double CiHigh,
    string Verdict,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Séance D : R̄ humain contre R̄ décodeur, <b>apparié par direction</b> — les deux ont vu le même
/// indice, les mêmes seize mots et le même ordre de présentation, donc la seule chose qui diffère
/// est <i>qui</i> devine.
/// <para>
/// Le verdict applique la règle <b>pré-enregistrée au registre le 2026-08-07</b>, avant que la
/// séance n'ait lieu. Il n'y a rien à décider ici : les trois issues et leur lecture étaient
/// écrites d'avance, c'est tout l'objet du pré-enregistrement.
/// </para>
/// </summary>
public static class GuessingComparison
{
    public const string VerdictInstrumentValide = "instrument valide — la cible est en cause";
    public const string VerdictDecodeurPlusFaible = "décodeur plus faible qu'un humain";
    public const string VerdictMontageAReexaminer = "montage à réexaminer";

    public static GuessingComparisonResult Compare(
        GuessingContents guessing,
        DecodeContents decoded,
        int iterations = PairedComparison.DefaultBootstrapIterations,
        long seed = PairedComparison.DefaultBootstrapSeed)
    {
        // Un décodage sans r est un échec de format, pas un échec sémantique : il sort de la
        // moyenne au lieu d'y entrer comme un zéro. Même règle que D6 côté taxonomie — une
        // direction dont aucun décodage n'est exploitable sort de l'appariement tout entier.
        var decoderRBar = decoded.ClueDecodes
            .Where(d => d.R.HasValue)
            .GroupBy(d => (d.BoardId, d.Direction))
            .ToDictionary(g => g.Key, g => g.Average(d => d.R!.Value));

        var paired = guessing.Guesses
            .Where(g => decoderRBar.ContainsKey((g.BoardId, g.Direction)))
            .OrderBy(g => g.BoardId, StringComparer.Ordinal)
            .ThenBy(g => g.Direction, StringComparer.Ordinal)
            .ToList();

        if (paired.Count == 0)
            throw new InvalidOperationException(
                "Aucune direction commune à la séance et au décodage : rien à comparer.");

        var deltas = paired
            .Select(g => g.R - decoderRBar[(g.BoardId, g.Direction)])
            .ToList();

        var (ciLow, ciHigh) = iterations < 1
            ? (deltas.Min(), deltas.Max())
            : Bootstrap.Ci(deltas, static xs => xs.Average(), iterations, seed);

        var reasons = new List<string>();
        string verdict;

        if (ciLow > 0)
        {
            verdict = VerdictDecodeurPlusFaible;
            reasons.Add(
                $"l'IC à 95 % est entièrement positif : l'humain devine mieux de " +
                $"{deltas.Average() * 100:0.0} pts. Une part non mesurable revient à l'avantage de " +
                "mémoire déclaré au pré-enregistrement (limite 5) — l'écart est un plafond, pas une valeur.");
        }
        else if (ciHigh < 0)
        {
            verdict = VerdictMontageAReexaminer;
            reasons.Add(
                "l'IC à 95 % est entièrement négatif : le décodeur devine mieux que l'humain. " +
                "Aucune interprétation n'est pré-autorisée pour cette issue.");
        }
        else
        {
            verdict = VerdictInstrumentValide;
            reasons.Add(
                "l'IC à 95 % contient zéro : humain et décodeur sont indistinguables sur la tâche du jeu.");
            reasons.Add(
                "Absence de preuve d'écart, jamais preuve d'équivalence — la séance ne détecte " +
                "qu'un écart supérieur à ~0,15 (puissance déclarée au pré-enregistrement).");
        }

        return new GuessingComparisonResult(
            PairedDirectionCount: paired.Count,
            HumanRecovery: paired.Average(g => g.R),
            DecoderRecovery: paired.Average(g => decoderRBar[(g.BoardId, g.Direction)]),
            Delta: deltas.Average(),
            CiLow: ciLow,
            CiHigh: ciHigh,
            Verdict: verdict,
            Reasons: reasons.AsReadOnly());
    }
}
