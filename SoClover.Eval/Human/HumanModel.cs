namespace SoClover.Eval.Human;

// ── Séance A ────────────────────────────────────────────────────────────────

/// <summary>Ligne 1 de <c>elicitation.*.jsonl</c> : trace de reproductibilité de la séance A.</summary>
public sealed record ElicitationManifest(
    string Kind,
    string BenchFile,
    string BenchHash,
    long Seed,
    int TargetCount,
    int TimerSeconds,
    int QuotaBeforePause,
    string? CandidatesRunId,
    int HarnessVersion,
    DateTime CreatedAtUtc);

/// <summary>
/// Une direction traitée. <b>Une ligne par item</b>, écrite à la soumission, <b>avant</b> la
/// révélation des candidats du modèle : une interruption ne peut donc jamais forcer à refaire un
/// item dont la cible et les candidats ont déjà été vus — ce serait une violation silencieuse
/// d'A-4, et la mesure de difficulté non assistée de cet item serait perdue sans trace.
/// <para>
/// <see cref="ElapsedSeconds"/> vient du chrono client, <see cref="ServerElapsedSeconds"/> est
/// mesuré entre <c>/api/next</c> et <c>/api/attempt</c>. Les deux sont consignés : <b>l'écart
/// entre les deux est l'audit du chrono</b>.
/// </para>
/// </summary>
public sealed record ElicitationLine(
    string Kind,
    string BoardId,
    string Direction,
    IReadOnlyList<string> ReferenceWords,
    string Outcome,
    string? Clue,
    int ElapsedSeconds,
    int ServerElapsedSeconds,
    string? RelationType,
    IReadOnlyList<string> RejectedAttempts,
    string SessionId,
    int ItemOrdinal,
    DateTime AuthoredAtUtc);

/// <summary>
/// Indice éventuellement ré-écrit APRÈS consultation des candidats du modèle. Ligne
/// <b>optionnelle et postérieure</b>, jointe par <c>(boardId, direction)</c>. C'est le matériau
/// few-shot le plus riche du corpus, et il reste <b>exclu du plafond humain</b>.
/// </summary>
public sealed record AssistLine(
    string Kind,
    string BoardId,
    string Direction,
    string? AssistedClue,
    string? Notes,
    DateTime AuthoredAtUtc);

public sealed record ElicitationContents(
    ElicitationManifest Manifest,
    IReadOnlyList<ElicitationLine> Elicitations,
    IReadOnlyList<AssistLine> Assists);

// ── Séance B ────────────────────────────────────────────────────────────────

public sealed record ComparisonRunRef(string RunId, string File);

public sealed record ComparisonManifest(
    string Kind,
    string BenchFile,
    string BenchHash,
    long Seed,
    string ElicitationFile,
    IReadOnlyList<ComparisonRunRef> Runs,
    int TargetCount,
    int QuotaBeforePause,
    double HoursSinceElicitation,
    bool EarlyStart,
    int HarnessVersion,
    DateTime CreatedAtUtc);

public sealed record ComparisonOption(string Source, string? RunId, string Clue);

/// <summary>
/// Un verdict. <b>Invariant à ne jamais confondre</b> : <see cref="OptionA"/> et
/// <see cref="OptionB"/> sont les slots <b>canoniques</b> (ordonnés par source, de façon
/// déterministe) ; <see cref="PresentedOrder"/> dit lequel a été affiché en <b>position 1</b> ;
/// <see cref="Verdict"/> désigne toujours l'option <b>canonique</b>, jamais la position.
/// Une inversion silencieuse ici rendrait faux à la fois le taux de victoire du modèle et le
/// contrôle de biais de position, sans qu'aucun test fonctionnel ne s'en aperçoive.
/// </summary>
public sealed record ComparisonLine(
    string Kind,
    string ComparisonId,
    string Family,
    string BoardId,
    string Direction,
    IReadOnlyList<string> ReferenceWords,
    ComparisonOption OptionA,
    ComparisonOption OptionB,
    string PresentedOrder,
    string Verdict,
    long ElapsedMs,
    string? DuplicateOf,
    string SessionId,
    int ItemOrdinal,
    DateTime JudgedAtUtc);

public sealed record ComparisonContents(
    ComparisonManifest Manifest,
    IReadOnlyList<ComparisonLine> Comparisons);

// ── Vocabulaires FERMÉS ─────────────────────────────────────────────────────

/// <summary>
/// Issues d'une direction. <c>tiede</c> sans accent : c'est un identifiant de données, pas du
/// texte affiché — le libellé accentué vit dans la page.
/// </summary>
public static class Outcomes
{
    public const string Solide = "solide";
    public const string Tiede = "tiede";
    public const string Pass = "pass";

    public static readonly IReadOnlyList<string> All = [Solide, Tiede, Pass];

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
}

/// <summary>
/// Vocabulaire fermé des relations : les 12 relations de l'étape 3 du prompt FR v5
/// (<c>board-clues-per-direction.md</c>), plus <c>R13_expression_figee</c> — l'étape 4, un
/// registre délibérément distinct des relations sémantiques — et <c>autre</c>.
/// <para>Une valeur hors de cette liste est un <c>400</c> : rien n'est consigné.</para>
/// </summary>
public static class RelationTypes
{
    public static readonly IReadOnlyList<string> All =
    [
        "R1_categorie",
        "R2_tout_partie",
        "R3_fonction_usage",
        "R4_lieu_contexte",
        "R5_cause_consequence",
        "R6_propriete_commune",
        "R7_opposition",
        "R8_sequence_temporalite",
        "R9_cooccurrence_culturelle",
        "R10_exemplaire_prototypique",
        "R11_polysemie",
        "R12_specialisation_croisee",
        "R13_expression_figee",
        "autre",
    ];

    private static readonly HashSet<string> Known = new(All, StringComparer.Ordinal);

    public static bool IsKnown(string value) => Known.Contains(value);
}

public static class ComparisonFamilies
{
    public const string HumanVsModel = "humanVsModel";
    public const string ModelVsModel = "modelVsModel";
    public const string HumanVsAssisted = "humanVsAssisted";
    public const string Anchor = "anchor";
}

/// <summary>
/// Provenances possibles d'un indice. <see cref="Rank"/> définit l'ordre <b>canonique</b> des
/// slots A/B d'un couple : c'est ce qui rend <c>optionA</c>/<c>optionB</c> indépendants de
/// l'ordre de présentation, donc le verdict interprétable.
/// </summary>
public static class ComparisonSources
{
    public const string Human = "human";
    public const string Assisted = "assisted";
    public const string Model = "model";
    public const string Random = "random";

    public static int Rank(string source) => source switch
    {
        Human => 0,
        Assisted => 1,
        Model => 2,
        Random => 3,
        _ => 4,
    };
}

public static class PresentedOrders
{
    public const string Ab = "AB";
    public const string Ba = "BA";

    public static string Invert(string order) => order == Ab ? Ba : Ab;
}
