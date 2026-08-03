# Calibration du décodeur et baseline officielle (P6 → P7) — Plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Livrer les deux verbes qui rendent les chiffres du harnais **publiables** — `calibrate` (accord décodeur/humain, Cohen's κ, quatre portes réunies en un verdict unique, statut `calibré` au registre) et `analyze` (taxonomie chiffrée des modes d'échec, validée contre 20 items lus à la main) — sans qu'aucune porte puisse être franchie sur un décodeur autre que celui qui a produit les chiffres.

**Architecture:** Deux espaces nouveaux dans `SoClover.Eval` : `Calibration/` porte l'empreinte de décodeur, la constitution du lot, l'accord/κ et les portes ; `Analysis/` porte la taxonomie et l'échantillon relu. Une seule extraction — `PairedComparison.BootstrapCi` devient `Scoring/Bootstrap.Ci`, paramétré par une statistique — pour que le Δ`recovery`, l'accord et κ partagent un unique intervalle de confiance. Les artefacts sont des JSONL append-only reprenables, du même contrat que `RunFile` et `DecodeFile`. Le décodage de calibration réutilise `ClueDecoder` **tel quel** : il décode un indice quelconque, il n'a jamais rien su du run dont l'indice provient.

**Tech Stack:** .NET 9 (`net9.0`), C# `latestmajor`, xunit 2.7, `System.Text.Json`, `Microsoft.Extensions.AI`. **Aucune nouvelle dépendance NuGet.** Aucun front touché.

**Spec source:** [`Specs/AI_Clue_Eval_Loop/03_Design_Calibration_P6_P7.md`](../../../Specs/AI_Clue_Eval_Loop/03_Design_Calibration_P6_P7.md), dérivé de [`00_Overview.md`](../../../Specs/AI_Clue_Eval_Loop/00_Overview.md). Cycles précédents : [`01_Design_Harness_P0_P3.md`](../../../Specs/AI_Clue_Eval_Loop/01_Design_Harness_P0_P3.md), [`02_Design_Human_P4_P5.md`](../../../Specs/AI_Clue_Eval_Loop/02_Design_Human_P4_P5.md).

---

## Global Constraints

- **Zéro octet d'éval en prod.** `SoClover/Dockerfile` ne référence que `SoClover/SoClover.csproj` ; `docs/deploy.md` fait `git archive HEAD … SoClover/`. **Ne jamais** ajouter `SoClover.Eval` au `Dockerfile`, ni placer du code d'éval sous `SoClover/`.
- **Aucune modification du code de production** (`SoClover/`) dans ce cycle. Les briques partagées ont été extraites en P0 et se consomment telles quelles : `Domain/BoardGeometry.cs`, `Domain/ClueAcceptance.cs`, `Infrastructure/AI/AiClueLlmCaller.cs`, `Infrastructure/AI/AiClueResponseParser.cs`, `Infrastructure/AI/Prompts/FilePromptLoader`.
- **Aucune réécriture des briques Eval existantes**, hors la seule extraction autorisée par le design (`Bootstrap`). Interdit de dupliquer : `ClueDecoder`, `ShuffleSeed`, `Xoshiro256SS`, `EvalJson`, `BenchFile.Read`, `HumanFile.ReadComparisons`, `HumanFile.LatestByComparisonId`, `SubsetSelector`, `RunMetrics.Compute`, `DecodeFile`, `LedgerWriter`.
- **Langue** : FR uniquement. Tout le texte visible (messages d'erreur, sorties console, fichiers `.sample.md`, commentaires de code) est en français, **accents inclus**.
- **Casse sensible** : `AI` et jamais `Ai` dans les chemins et namespaces. Attention à l'insensibilité à la casse de Windows au staging Git.
- **Reproductibilité** : `System.Random` est **interdit** (séquence non garantie stable entre versions du runtime) — `Xoshiro256SS` est la seule source d'aléa. `string.GetHashCode()` est **interdit** comme source de déterminisme (randomisé par processus) — passer par `EvalJson.Sha256Hex`.
- **Aucun appel LLM réel dans les tests.** Le `FakeChatClient` de `SoClover.Tests/Ai/FakeChatClient.cs` (namespace `SoClover.Tests.AI`) sert le décodeur de calibration comme il sert déjà `ClueDecoder`.
- **TDD strict** : test d'abord, exécution qui échoue, implémentation minimale, exécution qui passe, commit. **Un commit atomique par tâche minimum.**
- **Après chaque tâche** : `dotnet build` propre **et** `dotnet test` complet. Le front React n'est **pas** touché — ne pas lancer `npm`.
- **Non-régression** : toutes les suites `SoClover.Tests/Eval/` et `SoClover.Tests/Ai/` de P0-P5 passent **sans modification d'aucune assertion**. En particulier `PairedComparisonTests` (filet de l'extraction T2), `LedgerWriterTests` (18 colonnes), `ScoreCommandNotesTests`, `CommittedBenchIntegrityTests` (aucun banc committé ne bouge).
- **Seuils repris VERBATIM du PRD** — un seuil qui dérive d'une spec à l'autre est exactement ce que le registre est censé rendre impossible :
  - accord brut décodeur/humain, hors égalités : **≥ 0,75**
  - Cohen's κ : **≥ 0,40**
  - non-saturation (`recovery` sur indices humains `solide`) : **≤ 0,95**
  - plancher (`recovery` sur indices aléatoires) : **≤ 0,15**
  - dénominateur d'accord jugé fragile sous **40** couples
  - cohérence intra-juge sous **0,75** → porte inatteignable par construction
  - règle d'intervention de la taxonomie : **≥ 5 %**
  - accord auto ↔ humain de l'étiquetage, seuil indicatif : **≥ 0,70**
- **Constantes figées** :
  - `CalibrationFile.HarnessVersion = 1` (aligné sur `RunFile.HarnessVersion` et `HumanFile.HarnessVersion`)
  - `DecoderFingerprint.HexLength = 12` (même longueur que `benchHash`)
  - `CalibrateCommand.DefaultDecodesPerClue = 5` — le défaut de `decode` reste **3**, inchangé
  - `AgreementMetrics.DefaultEpsilon = 0.0`
  - `Bootstrap.DefaultIterations = 10_000`, `PairedComparison.DefaultBootstrapSeed = 20260727777` **inchangé**
  - `LedgerWriter.ColumnCount` reste **18** — aucune colonne nouvelle
- **Artefacts** : `eval/human/` est **committé** (le corpus humain et désormais les fichiers de calibration sont l'investissement irremplaçable du chantier). `eval/runs/` reste gitignoré. `eval/analysis/<runId>.sample.md` est **committé** ; `eval/analysis/<runId>.taxonomy.json` est dérivé, donc gitignoré. `eval/LEDGER.md` est committé et **jamais réécrit**.
- **Discipline dev/test** : ce cycle ne touche **que** `boards.dev.jsonl`. Aucune commande de ce plan ne prend `boards.test.jsonl` en entrée — la non-consultation du test set est un critère de fin de cycle.
- **Le code ne produit aucun chiffre tant que les séances humaines n'ont pas eu lieu.** `eval/human/` n'existe pas à ce jour. Toutes les tâches se développent et se testent sur **corpus fabriqués**. La §Exécution opérationnelle en fin de plan est hors périmètre des tâches de code.

---

## Décisions de plan (écarts assumés au design, à ne pas « corriger » en cours de route)

Le design laisse huit points sous-déterminés. Les arbitrages ci-dessous sont figés ; les rouvrir en cours d'implémentation produirait des artefacts incomparables.

| # | Point | Design | Décision du plan | Motif |
|---|---|---|---|---|
| D1 | `cluePromptFile` dans l'empreinte | « SHA-256 de la config canonicalisée » incluant `cluePromptFile` | On hache les **deux derniers segments** du chemin, en `/`, en minuscules : `fr/decode-clue.md` | `DecodeManifest.CluePromptFile` est un chemin **absolu** dérivé de `AppContext.BaseDirectory`. Haché tel quel, l'empreinte changerait d'une machine à l'autre, d'un `bin/Debug` à un `bin/Release` — et deux calibrations identiques deviendraient incomparables. Deux segments et pas un seul : `en/decode-clue.md` doit être une autre empreinte |
| D2 | Lecture de l'empreinte depuis un `.metrics.json` | « le verbe refuse si les métriques citées ne portent pas la même empreinte » | `MetricsReport` **ne porte aucune empreinte**. On dérive le `.decoded.jsonl` frère (`<run>.metrics.json` → `<run>.decoded.jsonl`) et on calcule l'empreinte depuis son `DecodeManifest` | Ajouter un champ à `MetricsReport` changerait le schéma des `.metrics.json` déjà produits et forcerait à modifier des assertions de P0-P3. Le fichier frère porte déjà toute l'information |
| D3 | Couple dont un indice n'a **aucun** décodage exploitable | non traité | Couple **exclu du calcul**, compté dans `UnscorableCoupleCount` et imprimé | R̄ indéfini d'un côté ne se compare pas. Le compter `tie` gonflerait le taux d'égalité du décodeur ; le compter 0 en ferait un perdant, alors que c'est le décodeur qui a échoué au format |
| D4 | κ quand `1 − p_e = 0` | non traité | κ = 0, et le rapport imprime la table de contingence qui le rend lisible | Marginales parfaitement dégénérées (les deux juges disent toujours `A`) : la division est indéfinie. Rendre 1 (« accord parfait ») serait le pire des deux mondes |
| D5 | `M4` avec un seul décodage exploitable | « `R̄ = 0` **et** aucun mot commun aux paires choisies aux différents décodages » | `M4` exige **≥ 2** décodages exploitables. Avec un seul, la direction tombe en `M?` | Avec une seule paire, « aucun mot commun » est vide de sens : l'intersection d'un singleton avec lui-même n'est jamais vide. Sans cette garde, `M4` absorberait silencieusement les directions mal décodées |
| D6 | Direction sans **aucun** décodage exploitable | non traité | Étiquetée `M?`, **et** comptée à part dans `UnscorableDirectionCount`, imprimée sur sa propre ligne | Une direction sans indice valide, ou dont les 3 décodages ont échoué au format, n'est pas un mode d'échec sémantique. La noyer dans `M?` ferait croire à une taxonomie incomplète alors que c'est le générateur ou le décodeur qui a échoué |
| D7 | Où figure l'empreinte dans la ligne de registre | « une ligne `calibré`, portant son empreinte » ; « 18 colonnes préservées » | Cellule **statut** = `calibré (3f2a91c4e0d1)`. `PreCalibrationStatus` et `ComposeNotes` **inchangés** | Toute autre colonne obligerait à modifier une assertion de `LedgerWriterTests` ou de `ScoreCommandNotesTests`, ce que la contrainte de non-régression interdit. Le statut est la cellule dont l'empreinte qualifie précisément le sens |
| D8 | `--force` de `calibrate` et `decodesPerClue` | « `--force` repart de zéro » | Relancer avec un `--decodes` différent de celui du manifeste **refuse**, comme `decode` le fait déjà (`DecodeCommand.cs:85`), et renvoie vers `--force` | Mélanger 3 et 5 décodages dans un même fichier produirait des R̄ de granularités différentes selon l'indice. La symétrie avec `decode` évite d'avoir deux règles de reprise à retenir |
| D9 | Ordre de priorité de la taxonomie | « `M2 → M3 → M1 → M4 → M?` », déclaré déterministe et documenté | **`M2 → M3 → M4 → M1 → M?`** | Sous l'ordre littéral, **M4 est structurellement inatteignable** : `R̄ = 0` impose deux mots faux par décodage, et une intersection vide sur ≥ 2 décodages impose ≥ 4 mots faux distincts — la signature M1 est donc toujours satisfaite d'abord. `R̄ = 0` est la condition strictement plus forte : elle passe devant. Un mode mort dans une taxonomie qui sert à arbitrer des interventions coûte plus cher qu'un ordre corrigé et écrit. **Seul écart de ce plan à une prescription littérale du design** — à répercuter dans le design en T10 |

---

## Structure des fichiers

### Créés dans `SoClover.Eval/`

| Fichier | Responsabilité |
|---|---|
| `Scoring/Bootstrap.cs` | IC bootstrap **partagé**, paramétré par une statistique. Rééchantillonnage avec remise, percentiles 2,5 % / 97,5 %, déterministe à seed fixé |
| `Calibration/DecoderFingerprint.cs` | Empreinte 12 hex de la config décodeur — ce qui rend deux `recovery` comparables. `decodesPerClue` en est **exclu** |
| `Calibration/CalibrationModel.cs` | Schémas JSONL : `CalibrationManifest`, `CalibrationDecode`, `CalibrationReport` + vocabulaire des portes |
| `Io/CalibrationFile.cs` | JSONL append-only reprenable, même contrat que `RunFile` / `DecodeFile` : garde `harnessVersion`, tolérance à la dernière ligne tronquée |
| `Calibration/CalibrationSet.cs` | `comparisons.dev.jsonl` → couples retenus (doublons inversés fusionnés, ancres à part) et indices distincts à décoder |
| `Calibration/AgreementMetrics.cs` | Verdict décodeur, accord hors égalités, κ, κ par famille, contingence, marginales, PABAK, IC bootstrap |
| `Calibration/CalibrationGates.cs` | Les **quatre** portes réunies en un verdict unique ; lecture des deux portes externes depuis les `.metrics.json` désignés ; refus sur empreintes divergentes |
| `Calibration/CalibrateCommand.cs` | Verbe `calibrate` : constitution du lot, décodage reprenable, agrégation, rapport |
| `Analysis/FailureTaxonomy.cs` | Signatures `M0`-`M6`, étiquette **unique** par direction, ordre de priorité déterministe, règle des 5 % |
| `Analysis/AnalysisSample.cs` | Échantillon seedé lisible (`.sample.md`), relecture des étiquettes humaines, matrice de confusion |
| `Analysis/AnalyzeCommand.cs` | Verbe `analyze` (`--sample`, `--seed`, `--review`) |

### Modifiés dans `SoClover.Eval/`

| Fichier | Modification |
|---|---|
| `Scoring/PairedComparison.cs` | `BootstrapCi` privé **supprimé**, délégué à `Bootstrap.Ci`. Aucun changement de comportement |
| `Io/LedgerWriter.cs` | `CalibratedStatus`, `CalibratedStatusFor(fingerprint)`, paragraphe du bandeau d'en-tête. `ColumnCount` **inchangé** |
| `Scoring/ScoreCommand.cs` | `--calibration <report.json>` : statut `calibré (<empreinte>)` **si et seulement si** quatre portes franchies **et** empreinte concordante ; refus bruyant sinon |
| `Program.cs` | Verbes `calibrate` et `analyze` + usage mis à jour |
| `README.md` | Sections calibration, taxonomie, ordre opérationnel P6-P7 |

### Modifiés à la racine

| Fichier | Modification |
|---|---|
| `.gitignore` | `eval/analysis/*.taxonomy.json` (dérivé) — `eval/analysis/*.sample.md` reste committé |
| `CLAUDE.md` | Verbes `calibrate` / `analyze`, statut `calibré`, empreinte de décodeur |
| `Specs/AI_Clue_Eval_Loop/03_Design_Calibration_P6_P7.md` | Statut : design → implémenté (clôture, T10) |

### Créés dans `SoClover.Tests/Eval/`

`BootstrapTests.cs`, `DecoderFingerprintTests.cs`, `CalibrationFileTests.cs`, `CalibrationSetTests.cs`, `AgreementMetricsTests.cs`, `CalibrationGatesTests.cs`, `ScoreCalibrationStatusTests.cs`, `FailureTaxonomyTests.cs`, `AnalysisSampleTests.cs`, plus l'extension de `Helpers/HumanTestData.cs` (fabriques de comparaisons et de décodages).

### Artefacts produits hors code (§Exécution opérationnelle)

`eval/human/calibration.<date>-<empreinte>.jsonl`, `eval/human/calibration.<date>-<empreinte>.json`, `eval/analysis/<runId>.sample.md`, deux lignes au moins dans `eval/LEDGER.md`.

---

## Séquencement

```
T1  document de design + ce plan (commit documentaire)
T2  Scoring/Bootstrap.cs ─────────────► LE REFACTOR EN PREMIER, PairedComparisonTests en filet
T3  CalibrationModel + DecoderFingerprint + Io/CalibrationFile ──► socle de persistance
T4  Calibration/CalibrationSet.cs ───► dépend de T3
T5  Calibration/AgreementMetrics.cs ─► dépend de T2, T4
T6  CalibrationGates + verbe calibrate ──► dépend de T3, T4, T5
T7  Statut `calibré` : LedgerWriter + score --calibration ──► dépend de T3, T6
T8  Analysis/FailureTaxonomy.cs ─────► indépendant (dépend de rien de P6)
T9  AnalysisSample + verbe analyze ──► dépend de T8
T10 documentation + clôture ─────────► dépend de tout
```

**T2 est en premier et non négociable.** Écrire `AgreementMetrics` avant l'extraction produirait un second bootstrap, donc deux IC légèrement différents pour la même raison, sans que personne ne sache lequel croire.

**T8 et T9 sont indépendants de T2-T7** : un agent peut les exécuter en parallèle de la chaîne de calibration si le plan est déroulé en subagents. T10 attend tout.

---

## Task 1 : Commit documentaire (design + plan)

**Files:**
- Create: `docs/superpowers/plans/2026-08-03-decoder-calibration-taxonomy-p6-p7.md` (ce plan, déjà écrit)
- Modify: `Specs/AI_Clue_Eval_Loop/03_Design_Calibration_P6_P7.md` (ligne de statut uniquement)

**Interfaces:**
- Consumes: rien
- Produces: rien de code

- [ ] **Step 1 : Pointer le plan depuis le design**

Dans `Specs/AI_Clue_Eval_Loop/03_Design_Calibration_P6_P7.md`, remplacer la première ligne du bandeau :

```markdown
> **Statut** : design validé, prêt pour plan d'implémentation. **Aucun code écrit.**
```

par :

```markdown
> **Statut** : design validé, plan d'implémentation écrit
> ([`2026-08-03-decoder-calibration-taxonomy-p6-p7.md`](../../docs/superpowers/plans/2026-08-03-decoder-calibration-taxonomy-p6-p7.md)).
> **Aucun code écrit.**
```

- [ ] **Step 2 : Commit**

```bash
git add docs/superpowers/plans/2026-08-03-decoder-calibration-taxonomy-p6-p7.md Specs/AI_Clue_Eval_Loop/03_Design_Calibration_P6_P7.md
git commit -m "docs(eval): plan d'implementation P6-P7 — calibration du decodeur et taxonomie"
```

---

## Task 2 : `Scoring/Bootstrap.cs` — l'extraction, en premier

Le refactor précède tout code neuf. `PairedComparisonTests` est le filet : **aucune de ses assertions ne change**, et les IC produits doivent rester identiques — c'est la seule preuve que l'extraction est sans effet.

**Files:**
- Create: `SoClover.Eval/Scoring/Bootstrap.cs`
- Modify: `SoClover.Eval/Scoring/PairedComparison.cs:130-159` (corps de `BootstrapCi` délégué, `Percentile` supprimée)
- Test: `SoClover.Tests/Eval/BootstrapTests.cs`
- Filet (non modifié) : `SoClover.Tests/Eval/PairedComparisonTests.cs`

**Interfaces:**
- Consumes: `SoClover.Eval.Bench.Xoshiro256SS` — `new Xoshiro256SS(long seed)`, `int NextInt(int exclusiveUpperBound)`
- Produces:
  - `SoClover.Eval.Scoring.Bootstrap.DefaultIterations` → `const int = 10_000`
  - `Bootstrap.Ci<T>(IReadOnlyList<T> sample, Func<IReadOnlyList<T>, double> statistic, int iterations, long seed)` → `(double Low, double High)`
  - `Bootstrap.Percentile(double[] sorted, double p)` → `double` (`internal`)

- [ ] **Step 1 : Écrire le test qui échoue**

Créer `SoClover.Tests/Eval/BootstrapTests.cs` :

```csharp
using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

public class BootstrapTests
{
    private static double Mean(IReadOnlyList<double> xs) => xs.Count == 0 ? 0.0 : xs.Average();

    [Fact]
    public void Is_deterministic_for_a_fixed_seed()
    {
        var sample = new[] { 0.0, 0.5, 1.0, 0.5, 0.0, 1.0 };

        var a = Bootstrap.Ci(sample, Mean, iterations: 500, seed: 99);
        var b = Bootstrap.Ci(sample, Mean, iterations: 500, seed: 99);

        Assert.Equal(a.Low, b.Low);
        Assert.Equal(a.High, b.High);
    }

    [Fact]
    public void A_different_seed_gives_a_different_interval()
    {
        var sample = new[] { 0.0, 0.5, 1.0, 0.5, 0.0, 1.0 };

        var a = Bootstrap.Ci(sample, Mean, iterations: 500, seed: 99);
        var b = Bootstrap.Ci(sample, Mean, iterations: 500, seed: 100);

        Assert.True(a.Low != b.Low || a.High != b.High);
    }

    [Fact]
    public void The_interval_brackets_the_statistic_of_the_original_sample()
    {
        var sample = Enumerable.Range(0, 60).Select(i => i % 2 == 0 ? 0.4 : 0.6).ToList();

        var (low, high) = Bootstrap.Ci(sample, Mean, iterations: 2_000, seed: 7);

        Assert.True(low <= 0.5);
        Assert.True(high >= 0.5);
    }

    // La statistique est un paramètre, pas une moyenne codée en dur : c'est toute la raison
    // de l'extraction — l'accord est une proportion, κ n'est ni l'une ni l'autre.
    [Fact]
    public void Accepts_a_proportion_statistic_and_brackets_it()
    {
        var sample = Enumerable.Range(0, 100).Select(i => i < 80).ToList();
        static double Share(IReadOnlyList<bool> xs) =>
            xs.Count == 0 ? 0.0 : xs.Count(x => x) / (double)xs.Count;

        var (low, high) = Bootstrap.Ci(sample, Share, iterations: 2_000, seed: 11);

        Assert.True(low <= 0.80 && 0.80 <= high);
        Assert.True(low > 0.60 && high < 0.95);
    }

    [Fact]
    public void A_constant_sample_yields_a_degenerate_interval()
    {
        var sample = Enumerable.Repeat(0.5, 30).ToList();

        var (low, high) = Bootstrap.Ci(sample, Mean, iterations: 500, seed: 3);

        Assert.Equal(0.5, low, precision: 10);
        Assert.Equal(0.5, high, precision: 10);
    }

    [Fact]
    public void Refuses_an_empty_sample()
    {
        Assert.Throws<ArgumentException>(
            () => Bootstrap.Ci(Array.Empty<double>(), Mean, iterations: 10, seed: 1));
    }

    // Le repli « min/max » de PairedComparison reste chez lui : ici, moins d'une itération
    // est une erreur d'appel, pas un mode dégradé silencieux.
    [Fact]
    public void Refuses_fewer_than_one_iteration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Bootstrap.Ci(new[] { 0.5 }, Mean, iterations: 0, seed: 1));
    }
}
```

- [ ] **Step 2 : Exécuter, vérifier l'échec**

Run : `dotnet test --filter "FullyQualifiedName~BootstrapTests"`
Attendu : **échec de compilation** — le type `Bootstrap` n'existe pas.

- [ ] **Step 3 : Écrire `Scoring/Bootstrap.cs`**

```csharp
using SoClover.Eval.Bench;

namespace SoClover.Eval.Scoring;

/// <summary>
/// Intervalle de confiance bootstrap, <b>partagé</b> par toutes les statistiques du harnais :
/// Δ<c>recovery</c> (moyenne des deltas appariés), accord décodeur/humain (proportion), Cohen's κ.
/// <para>
/// Un seul rééchantillonnage pour tout le monde, et pas trois : dupliquer un bootstrap est le
/// genre de dette qui produit ensuite deux IC légèrement différents pour la même raison, sans
/// que personne ne sache lequel croire.
/// </para>
/// <para>
/// Déterministe à seed fixé : <see cref="Xoshiro256SS"/> et jamais <c>System.Random</c>, dont la
/// séquence n'est pas garantie stable entre versions du runtime.
/// </para>
/// </summary>
public static class Bootstrap
{
    public const int DefaultIterations = 10_000;

    /// <summary>
    /// Rééchantillonne <paramref name="sample"/> avec remise, <paramref name="iterations"/> fois,
    /// applique <paramref name="statistic"/> à chaque rééchantillon, et rend les percentiles
    /// 2,5 % et 97,5 % des valeurs obtenues.
    /// <para>
    /// L'ordre des tirages reproduit celui de l'implémentation historique de
    /// <c>PairedComparison.BootstrapCi</c> — <c>sample.Count</c> appels à <c>NextInt</c> par
    /// itération, dans l'ordre — pour que les IC déjà publiés au registre restent reproductibles
    /// à l'identique après l'extraction.
    /// </para>
    /// </summary>
    public static (double Low, double High) Ci<T>(
        IReadOnlyList<T> sample,
        Func<IReadOnlyList<T>, double> statistic,
        int iterations,
        long seed)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(statistic);

        if (sample.Count == 0)
            throw new ArgumentException(
                "Un intervalle de confiance sur un échantillon vide n'a pas de sens.", nameof(sample));

        if (iterations < 1)
            throw new ArgumentOutOfRangeException(
                nameof(iterations), iterations, "Le nombre d'itérations doit être ≥ 1.");

        var rng = new Xoshiro256SS(seed);
        var values = new double[iterations];
        var resample = new T[sample.Count];

        for (var i = 0; i < iterations; i++)
        {
            for (var j = 0; j < sample.Count; j++)
                resample[j] = sample[rng.NextInt(sample.Count)];

            values[i] = statistic(resample);
        }

        Array.Sort(values);
        return (Percentile(values, 0.025), Percentile(values, 0.975));
    }

    internal static double Percentile(double[] sorted, double p)
    {
        var index = (int)Math.Clamp(Math.Round(p * (sorted.Length - 1)), 0, sorted.Length - 1);
        return sorted[index];
    }
}
```

- [ ] **Step 4 : Exécuter, vérifier le passage**

Run : `dotnet test --filter "FullyQualifiedName~BootstrapTests"`
Attendu : **7 tests PASS**.

- [ ] **Step 5 : Déléguer depuis `PairedComparison`**

Dans `SoClover.Eval/Scoring/PairedComparison.cs`, l'appel ligne 74 reste **inchangé** :

```csharp
        var (ciLow, ciHigh) = BootstrapCi(deltas, bootstrapIterations, seed);
```

Remplacer **intégralement** les deux méthodes privées `BootstrapCi` et `Percentile` (lignes 130-159) par cette seule méthode :

```csharp
    /// <summary>
    /// Bootstrap apparié : rééchantillonnage des <b>items</b> avec remise. Délègue à
    /// <see cref="Bootstrap.Ci{T}"/> — le repli « moins d'une itération » reste ici : c'est une
    /// convention propre à ce verbe (rendre l'étendue observée plutôt que de refuser), pas un
    /// comportement que doit porter la brique partagée.
    /// </summary>
    private static (double Low, double High) BootstrapCi(
        IReadOnlyList<double> deltas, int iterations, long seed) =>
        iterations < 1
            ? (deltas.Min(), deltas.Max())
            : Bootstrap.Ci(deltas, static xs => xs.Average(), iterations, seed);
```

Le `using SoClover.Eval.Bench;` en tête de `PairedComparison.cs` devient inutile (`Xoshiro256SS` n'y est plus référencé) — le supprimer.

- [ ] **Step 6 : Vérifier la non-régression, sans toucher une assertion**

Run : `dotnet test --filter "FullyQualifiedName~PairedComparisonTests"`
Attendu : **14 tests PASS**, avec `PairedComparisonTests.cs` **non modifié** — en particulier
`Bootstrap_is_deterministic_for_a_fixed_seed` et `Confidence_interval_brackets_the_delta`.

- [ ] **Step 7 : Suite complète et build**

Run : `dotnet build` puis `dotnet test`
Attendu : build propre, suite entière verte.

- [ ] **Step 8 : Commit**

```bash
git add SoClover.Eval/Scoring/Bootstrap.cs SoClover.Eval/Scoring/PairedComparison.cs SoClover.Tests/Eval/BootstrapTests.cs
git commit -m "refactor(eval): extraction de Bootstrap.Ci — un seul IC pour recovery, accord et kappa"
```

---

## Task 3 : Schémas, empreinte de décodeur, persistance

Le socle. L'empreinte est le constat déclencheur de tout le cycle : les `.decoded.jsonl` committés portent `cluePromptVersion: 1`, le prompt est aujourd'hui en v2 — **le décodeur qui a produit `recovery = 0,363` n'existe plus**. Ce cas ne doit plus pouvoir se reproduire silencieusement.

**Files:**
- Create: `SoClover.Eval/Calibration/CalibrationModel.cs`
- Create: `SoClover.Eval/Calibration/DecoderFingerprint.cs`
- Create: `SoClover.Eval/Io/CalibrationFile.cs`
- Test: `SoClover.Tests/Eval/DecoderFingerprintTests.cs`
- Test: `SoClover.Tests/Eval/CalibrationFileTests.cs`

**Interfaces:**
- Consumes:
  - `SoClover.Eval.Decoder.DecodeManifest` — champs `ModelId`, `CluePromptFile`, `CluePromptVersion`, `Temperature`, `TopP`, `MaxOutputTokens`, `DecodesPerClue`
  - `SoClover.Eval.Decoder.ClueDecodeLine` — `Kind`, `BoardId`, `Direction`, `DecodeIndex`, `Picked`, `R`, `ShuffleSeed`, `DecodeFailureKind`, `LatencyMs`
  - `SoClover.Eval.Io.EvalJson` — `Serialize<T>`, `Deserialize<T>`, `Sha256Hex`
  - `SoClover.Eval.Io.RunIntegrityException`
- Produces:
  - `SoClover.Eval.Calibration.DecoderFingerprint.HexLength` → `const int = 12`
  - `DecoderFingerprint.Compute(string modelId, string cluePromptFile, int? cluePromptVersion, double temperature, double? topP, int? maxOutputTokens)` → `string`
  - `DecoderFingerprint.FromManifest(DecodeManifest manifest)` → `string`
  - `DecoderFingerprint.CanonicalPromptPath(string path)` → `string` (`internal`)
  - `SoClover.Eval.Calibration.CalibrationManifest`, `CalibrationDecode`, `CalibrationContents` (records)
  - `CalibrationDecode.From(ClueDecodeLine line, string clue)` → `CalibrationDecode`
  - `SoClover.Eval.Io.CalibrationFile.HarnessVersion` → `const int = 1`
  - `CalibrationFile.PathFor(string directory, string calibrationId)` → `string`
  - `CalibrationFile.ReportPathFor(string jsonlPath)` → `string`
  - `CalibrationFile.WriteManifest(string path, CalibrationManifest manifest)` → `void`
  - `CalibrationFile.AppendDecode(string path, CalibrationDecode decode)` → `void`
  - `CalibrationFile.Read(string path)` → `CalibrationContents`
  - `CalibrationFile.ReadOrNull(string path)` → `CalibrationContents?`

- [ ] **Step 1 : Écrire le test d'empreinte qui échoue**

Créer `SoClover.Tests/Eval/DecoderFingerprintTests.cs` :

```csharp
using SoClover.Eval.Calibration;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using Xunit;

namespace SoClover.Tests.Eval;

public class DecoderFingerprintTests
{
    private static string Fingerprint(
        string modelId = "qwen/qwen3-8b",
        string promptFile = "C:/build/Decoder/Prompts/fr/decode-clue.md",
        int? version = 2,
        double temperature = 0.3,
        double? topP = null,
        int? maxOutputTokens = 512) =>
        DecoderFingerprint.Compute(modelId, promptFile, version, temperature, topP, maxOutputTokens);

    [Fact]
    public void Is_twelve_lowercase_hex_characters()
    {
        var fingerprint = Fingerprint();

        Assert.Equal(DecoderFingerprint.HexLength, fingerprint.Length);
        Assert.Matches("^[0-9a-f]{12}$", fingerprint);
    }

    [Fact]
    public void Is_stable_for_an_identical_configuration()
    {
        Assert.Equal(Fingerprint(), Fingerprint());
    }

    [Theory]
    [InlineData("autre-modele", null, null, null, null)]
    [InlineData(null, "C:/build/Decoder/Prompts/en/decode-clue.md", null, null, null)]
    [InlineData(null, null, 3, null, null)]
    [InlineData(null, null, null, 0.7, null)]
    [InlineData(null, null, null, null, 1024)]
    public void Changes_on_each_of_the_configuration_fields(
        string? modelId, string? promptFile, int? version, double? temperature, int? maxOutputTokens)
    {
        var changed = DecoderFingerprint.Compute(
            modelId ?? "qwen/qwen3-8b",
            promptFile ?? "C:/build/Decoder/Prompts/fr/decode-clue.md",
            version ?? 2,
            temperature ?? 0.3,
            null,
            maxOutputTokens ?? 512);

        Assert.NotEqual(Fingerprint(), changed);
    }

    [Fact]
    public void Changes_when_topP_moves_from_null_to_a_value()
    {
        Assert.NotEqual(Fingerprint(), Fingerprint(topP: 0.95));
    }

    // C'EST LE POINT DU CYCLE : le décodeur qui a produit recovery = 0,363 portait
    // cluePromptVersion 1 ; le prompt est en v2. Deux recovery d'empreintes différentes ne
    // se comparent pas, au même titre que deux runs de benchHash différents.
    [Fact]
    public void The_prompt_version_alone_changes_the_fingerprint()
    {
        Assert.NotEqual(Fingerprint(version: 1), Fingerprint(version: 2));
    }

    // decodesPerClue change la GRANULARITÉ de R̄, pas le décodeur : c'est ce qui autorise une
    // calibration à 5 décodages et des runs à 3 sans que les empreintes divergent.
    [Fact]
    public void The_number_of_decodes_per_clue_is_deliberately_excluded()
    {
        var three = Manifest(decodesPerClue: 3);
        var five = Manifest(decodesPerClue: 5);

        Assert.Equal(DecoderFingerprint.FromManifest(three), DecoderFingerprint.FromManifest(five));
    }

    // Le chemin du prompt est ABSOLU dans le manifeste (dérivé d'AppContext.BaseDirectory).
    // Haché tel quel, l'empreinte changerait d'une machine à l'autre — et deux calibrations
    // identiques deviendraient incomparables.
    [Fact]
    public void Two_machines_with_different_base_directories_share_the_fingerprint()
    {
        var windows = Fingerprint(promptFile: "C:/src/bin/Debug/net9.0/Decoder/Prompts/fr/decode-clue.md");
        var linux = Fingerprint(promptFile: "/home/ci/app/Decoder/Prompts/fr/decode-clue.md");

        Assert.Equal(windows, linux);
    }

    [Fact]
    public void Backslashes_and_casing_do_not_change_the_canonical_prompt_path()
    {
        Assert.Equal(
            DecoderFingerprint.CanonicalPromptPath(@"C:\build\Decoder\Prompts\FR\Decode-Clue.md"),
            DecoderFingerprint.CanonicalPromptPath("/opt/app/Decoder/Prompts/fr/decode-clue.md"));
    }

    [Fact]
    public void A_bare_file_name_is_accepted_as_its_own_canonical_path()
    {
        Assert.Equal("decode-clue.md", DecoderFingerprint.CanonicalPromptPath("decode-clue.md"));
    }

    [Fact]
    public void FromManifest_matches_Compute_on_the_same_fields()
    {
        var manifest = Manifest();

        Assert.Equal(
            DecoderFingerprint.Compute(
                manifest.ModelId, manifest.CluePromptFile, manifest.CluePromptVersion,
                manifest.Temperature, manifest.TopP, manifest.MaxOutputTokens),
            DecoderFingerprint.FromManifest(manifest));
    }

    internal static DecodeManifest Manifest(
        string modelId = "qwen/qwen3-8b",
        int? cluePromptVersion = 2,
        int decodesPerClue = 3,
        double temperature = 0.3) => new(
        Kind: "manifest",
        DecodeRunId: "run+decode-20260805120000",
        CreatedAtUtc: new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc),
        GeneratorRunId: "run",
        BenchFile: "eval/boards.dev.jsonl",
        BenchHash: "416b819a41a1",
        Provider: "OpenAI",
        BaseUrl: "http://localhost:1234/v1",
        ModelId: modelId,
        ModelSnapshotDate: "2026-08-05",
        ProviderModelListHash: "0123456789ab",
        Temperature: temperature,
        TopP: null,
        MaxOutputTokens: 512,
        CluePromptFile: "C:/build/Decoder/Prompts/fr/decode-clue.md",
        CluePromptVersion: cluePromptVersion,
        BoardPromptFile: "C:/build/Decoder/Prompts/fr/decode-board.md",
        BoardPromptVersion: 1,
        DecodesPerClue: decodesPerClue,
        HarnessVersion: RunFile.HarnessVersion,
        OperatorNotes: null);
}
```

- [ ] **Step 2 : Exécuter, vérifier l'échec**

Run : `dotnet test --filter "FullyQualifiedName~DecoderFingerprintTests"`
Attendu : **échec de compilation** — `DecoderFingerprint` n'existe pas.

- [ ] **Step 3 : Écrire `Calibration/DecoderFingerprint.cs`**

```csharp
using System.Globalization;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;

namespace SoClover.Eval.Calibration;

/// <summary>
/// Empreinte de la configuration du décodeur : ce qui rend deux <c>recovery</c> comparables.
/// <para>
/// Le constat déclencheur du cycle P6 : les <c>.decoded.jsonl</c> committés portent
/// <c>cluePromptVersion: 1</c> alors que <c>decode-clue.md</c> est en v2. <b>Le décodeur qui a
/// produit <c>recovery = 0,363</c> n'existe plus.</b> Sans empreinte, une porte serait franchie
/// sur un instrument et des chiffres publiés par un autre.
/// </para>
/// <para>
/// <c>decodesPerClue</c> en est <b>délibérément exclu</b> : il change la granularité de R̄, pas le
/// décodeur. C'est ce qui autorise une calibration à 5 décodages et des runs à 3 sans que les
/// empreintes divergent. Le nombre de décodages reste consigné à part, dans les deux manifestes.
/// </para>
/// </summary>
public static class DecoderFingerprint
{
    /// <summary>Même longueur que <c>benchHash</c> : les deux se lisent côte à côte au registre.</summary>
    public const int HexLength = 12;

    /// <summary>Séparateur de champs du texte haché : U+001F, impossible dans un identifiant de modèle.</summary>
    private const string FieldSeparator = "\u001F";

    private const string Absent = "—";

    public static string Compute(
        string modelId,
        string cluePromptFile,
        int? cluePromptVersion,
        double temperature,
        double? topP,
        int? maxOutputTokens)
    {
        string[] fields =
        [
            modelId,
            CanonicalPromptPath(cluePromptFile),
            cluePromptVersion?.ToString(CultureInfo.InvariantCulture) ?? Absent,
            temperature.ToString("R", CultureInfo.InvariantCulture),
            topP?.ToString("R", CultureInfo.InvariantCulture) ?? Absent,
            maxOutputTokens?.ToString(CultureInfo.InvariantCulture) ?? Absent,
        ];

        return EvalJson.Sha256Hex(string.Join(FieldSeparator, fields))[..HexLength];
    }

    public static string FromManifest(DecodeManifest manifest) => Compute(
        manifest.ModelId,
        manifest.CluePromptFile,
        manifest.CluePromptVersion,
        manifest.Temperature,
        manifest.TopP,
        manifest.MaxOutputTokens);

    /// <summary>
    /// Les <b>deux derniers segments</b> du chemin, en <c>/</c> et en minuscules :
    /// <c>fr/decode-clue.md</c>.
    /// <para>
    /// <c>DecodeManifest.CluePromptFile</c> est un chemin <b>absolu</b> dérivé de
    /// <c>AppContext.BaseDirectory</c>. Haché tel quel, l'empreinte changerait d'une machine à
    /// l'autre, et même d'un <c>bin/Debug</c> à un <c>bin/Release</c> : deux calibrations
    /// identiques deviendraient incomparables. Deux segments et non un seul, parce que
    /// <c>en/decode-clue.md</c> doit être une autre empreinte.
    /// </para>
    /// </summary>
    internal static string CanonicalPromptPath(string path)
    {
        var segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var tail = segments.TakeLast(2);
        return string.Join('/', tail).ToLowerInvariant();
    }
}
```

- [ ] **Step 4 : Exécuter, vérifier le passage**

Run : `dotnet test --filter "FullyQualifiedName~DecoderFingerprintTests"`
Attendu : **14 tests PASS** (5 cas du `[Theory]` compris).

- [ ] **Step 5 : Écrire le test de persistance qui échoue**

Créer `SoClover.Tests/Eval/CalibrationFileTests.cs` :

```csharp
using SoClover.Eval.Calibration;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using Xunit;

namespace SoClover.Tests.Eval;

public class CalibrationFileTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"calibration-{Guid.NewGuid():N}.jsonl");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static CalibrationManifest Manifest(
        int harnessVersion = CalibrationFile.HarnessVersion,
        string benchHash = "416b819a41a1",
        string fingerprint = "3f2a91c4e0d1",
        int decodesPerClue = 5,
        double epsilon = 0.0) => new(
        Kind: "manifest",
        CalibrationId: $"20260805-{fingerprint}",
        CreatedAtUtc: new DateTime(2026, 8, 5, 9, 0, 0, DateTimeKind.Utc),
        ComparisonsFile: "eval/human/comparisons.dev.jsonl",
        BenchFile: "eval/boards.dev.jsonl",
        BenchHash: benchHash,
        CoupleCount: 100,
        ClueCount: 187,
        DecoderFingerprint: fingerprint,
        Provider: "OpenAI",
        BaseUrl: "http://localhost:1234/v1",
        ModelId: "qwen/qwen3-8b",
        ModelSnapshotDate: "2026-08-05",
        ProviderModelListHash: "0123456789ab",
        Temperature: 0.3,
        TopP: null,
        MaxOutputTokens: 512,
        CluePromptFile: "C:/build/Decoder/Prompts/fr/decode-clue.md",
        CluePromptVersion: 2,
        DecodesPerClue: decodesPerClue,
        Epsilon: epsilon,
        HarnessVersion: harnessVersion,
        OperatorNotes: "qwen3-8b thinking OFF, ctx 8k");

    private static CalibrationDecode Decode(
        string boardId = "dev-007", string direction = "Top", string clue = "Pédiatre",
        int decodeIndex = 0, double? r = 1.0, string? failureKind = null) => new(
        Kind: "calibrationDecode",
        BoardId: boardId,
        Direction: direction,
        Clue: clue,
        DecodeIndex: decodeIndex,
        Picked: failureKind is null ? ["Chirurgien", "Enfant"] : null,
        R: failureKind is null ? r : null,
        ShuffleSeed: "-123456789",
        DecodeFailureKind: failureKind,
        LatencyMs: 2900);

    [Fact]
    public void Round_trips_the_manifest_and_the_decodes()
    {
        CalibrationFile.WriteManifest(_path, Manifest());
        CalibrationFile.AppendDecode(_path, Decode());
        CalibrationFile.AppendDecode(_path, Decode(decodeIndex: 1, r: 0.5));

        var contents = CalibrationFile.Read(_path);

        Assert.Equal("20260805-3f2a91c4e0d1", contents.Manifest.CalibrationId);
        Assert.Equal(5, contents.Manifest.DecodesPerClue);
        Assert.Equal(0.0, contents.Manifest.Epsilon);
        Assert.Equal(2, contents.Decodes.Count);
        Assert.Equal("Pédiatre", contents.Decodes[0].Clue);
        Assert.Equal(0.5, contents.Decodes[1].R);
    }

    // LE point du type dédié : en calibration une direction porte DEUX à TROIS indices
    // concurrents. Sous la clé (boardId, direction, decodeIndex) de ClueDecodeLine, ils
    // collisionneraient silencieusement à la reprise.
    [Fact]
    public void Two_distinct_clues_of_the_same_direction_do_not_collide()
    {
        CalibrationFile.WriteManifest(_path, Manifest());
        CalibrationFile.AppendDecode(_path, Decode(clue: "Pédiatre", decodeIndex: 0));
        CalibrationFile.AppendDecode(_path, Decode(clue: "Hôpital", decodeIndex: 0));

        var contents = CalibrationFile.Read(_path);
        var keys = contents.Decodes
            .Select(d => (d.BoardId, d.Direction, d.Clue, d.DecodeIndex))
            .Distinct()
            .ToList();

        Assert.Equal(2, contents.Decodes.Count);
        Assert.Equal(2, keys.Count);
    }

    [Fact]
    public void A_failed_decode_carries_no_picked_and_no_r()
    {
        CalibrationFile.WriteManifest(_path, Manifest());
        CalibrationFile.AppendDecode(_path, Decode(failureKind: "unparseable"));

        var decode = CalibrationFile.Read(_path).Decodes.Single();

        Assert.Equal("unparseable", decode.DecodeFailureKind);
        Assert.Null(decode.R);
        Assert.Null(decode.Picked);
    }

    // Même religion que RunFile et DecodeFile : une séance interrompue en pleine écriture
    // ne doit pas rendre le fichier illisible — la reprise regénérera la ligne.
    [Fact]
    public void Tolerates_a_last_line_truncated_mid_write()
    {
        CalibrationFile.WriteManifest(_path, Manifest());
        CalibrationFile.AppendDecode(_path, Decode());
        File.AppendAllText(_path, "{\"kind\":\"calibrationDecode\",\"boardId\":\"dev-0");

        var contents = CalibrationFile.Read(_path);

        Assert.Single(contents.Decodes);
    }

    [Fact]
    public void Refuses_an_incompatible_harness_version()
    {
        CalibrationFile.WriteManifest(_path, Manifest(harnessVersion: 99));

        var ex = Assert.Throws<RunIntegrityException>(() => CalibrationFile.Read(_path));
        Assert.Contains("harnessVersion", ex.Message);
    }

    [Fact]
    public void Refuses_a_file_whose_first_line_is_not_a_manifest()
    {
        File.WriteAllText(_path, EvalJson.Serialize(Decode()) + "\n");

        Assert.Throws<RunIntegrityException>(() => CalibrationFile.Read(_path));
    }

    [Fact]
    public void Refuses_an_unknown_kind()
    {
        CalibrationFile.WriteManifest(_path, Manifest());
        File.AppendAllText(_path, "{\"kind\":\"decode\",\"boardId\":\"dev-007\"}\n");
        File.AppendAllText(_path, EvalJson.Serialize(Decode()) + "\n");

        var ex = Assert.Throws<RunIntegrityException>(() => CalibrationFile.Read(_path));
        Assert.Contains("kind", ex.Message);
    }

    [Fact]
    public void ReadOrNull_returns_null_when_the_file_is_absent()
    {
        Assert.Null(CalibrationFile.ReadOrNull(_path));
    }

    [Fact]
    public void PathFor_names_the_file_after_the_calibration_id()
    {
        var path = CalibrationFile.PathFor("eval/human", "20260805-3f2a91c4e0d1");

        Assert.Equal("calibration.20260805-3f2a91c4e0d1.jsonl", Path.GetFileName(path));
    }

    [Fact]
    public void ReportPathFor_swaps_the_jsonl_extension_for_json()
    {
        var report = CalibrationFile.ReportPathFor(
            CalibrationFile.PathFor("eval/human", "20260805-3f2a91c4e0d1"));

        Assert.Equal("calibration.20260805-3f2a91c4e0d1.json", Path.GetFileName(report));
    }

    [Fact]
    public void From_projects_a_clue_decode_line_and_adds_the_clue()
    {
        var line = new ClueDecodeLine(
            Kind: "decode", BoardId: "dev-007", Direction: "Top", DecodeIndex: 2,
            Picked: ["Chirurgien", "Enfant"], R: 1.0, ShuffleSeed: "42",
            DecodeFailureKind: null, LatencyMs: 1234);

        var decode = CalibrationDecode.From(line, "Pédiatre");

        Assert.Equal("calibrationDecode", decode.Kind);
        Assert.Equal("Pédiatre", decode.Clue);
        Assert.Equal("dev-007", decode.BoardId);
        Assert.Equal("Top", decode.Direction);
        Assert.Equal(2, decode.DecodeIndex);
        Assert.Equal(1.0, decode.R);
        Assert.Equal("42", decode.ShuffleSeed);
        Assert.Equal(1234, decode.LatencyMs);
    }
}
```

- [ ] **Step 6 : Exécuter, vérifier l'échec**

Run : `dotnet test --filter "FullyQualifiedName~CalibrationFileTests"`
Attendu : **échec de compilation** — `CalibrationManifest`, `CalibrationDecode`, `CalibrationFile` n'existent pas.

- [ ] **Step 7 : Écrire `Calibration/CalibrationModel.cs`**

```csharp
using SoClover.Eval.Decoder;

namespace SoClover.Eval.Calibration;

/// <summary>
/// Ligne 1 de <c>eval/human/calibration.&lt;date&gt;-&lt;empreinte&gt;.jsonl</c> : la trace de
/// reproductibilité de la calibration.
/// <para>
/// <see cref="Epsilon"/> y figure parce que ce <b>n'est pas un réglage libre</b> : le modifier
/// après avoir vu l'accord serait ajuster l'instrument sur sa propre mesure. Toute valeur non
/// nulle doit être décidée <i>avant</i> de lire le résultat — et l'artefact la rend visible.
/// </para>
/// </summary>
public sealed record CalibrationManifest(
    string Kind,
    string CalibrationId,
    DateTime CreatedAtUtc,
    string ComparisonsFile,
    string BenchFile,
    string BenchHash,
    int CoupleCount,
    int ClueCount,
    string DecoderFingerprint,
    string Provider,
    string BaseUrl,
    string ModelId,
    string? ModelSnapshotDate,
    string? ProviderModelListHash,
    double Temperature,
    double? TopP,
    int? MaxOutputTokens,
    string CluePromptFile,
    int? CluePromptVersion,
    int DecodesPerClue,
    double Epsilon,
    int HarnessVersion,
    string? OperatorNotes);

/// <summary>
/// Un décodage de calibration. C'est <see cref="ClueDecodeLine"/> <b>plus le champ
/// <see cref="Clue"/></b>, et le champ n'est pas décoratif : la clé de reprise devient
/// <c>(boardId, direction, clue, decodeIndex)</c>.
/// <para>
/// <see cref="ClueDecodeLine"/> a pour clé <c>(boardId, direction, decodeIndex)</c> — elle suffit
/// à un run, où une direction porte un seul indice. En calibration, une direction porte <b>deux à
/// trois</b> indices concurrents (humain, modèle, assisté) : la réutiliser telle quelle les ferait
/// collisionner silencieusement à la reprise.
/// </para>
/// </summary>
public sealed record CalibrationDecode(
    string Kind,
    string BoardId,
    string Direction,
    string Clue,
    int DecodeIndex,
    IReadOnlyList<string>? Picked,
    double? R,
    string ShuffleSeed,
    string? DecodeFailureKind,
    long LatencyMs)
{
    public const string LineKind = "calibrationDecode";

    /// <summary>
    /// Projette le résultat de <c>ClueDecoder.DecodeAsync</c> — réutilisé <b>tel quel</b> : il
    /// décode un indice quelconque, il n'a jamais rien su du run dont l'indice provient.
    /// </summary>
    public static CalibrationDecode From(ClueDecodeLine line, string clue) => new(
        Kind: LineKind,
        BoardId: line.BoardId,
        Direction: line.Direction,
        Clue: clue,
        DecodeIndex: line.DecodeIndex,
        Picked: line.Picked,
        R: line.R,
        ShuffleSeed: line.ShuffleSeed,
        DecodeFailureKind: line.DecodeFailureKind,
        LatencyMs: line.LatencyMs);
}

public sealed record CalibrationContents(
    CalibrationManifest Manifest,
    IReadOnlyList<CalibrationDecode> Decodes);
```

- [ ] **Step 8 : Écrire `Io/CalibrationFile.cs`**

```csharp
using System.Text;
using System.Text.Json;
using SoClover.Eval.Calibration;

namespace SoClover.Eval.Io;

/// <summary>
/// Fichier de calibration <c>calibration.&lt;date&gt;-&lt;empreinte&gt;.jsonl</c> : ligne 1 =
/// manifeste, lignes suivantes = <c>calibrationDecode</c>. Append-only, reprenable, jamais
/// réécrit — même contrat que <see cref="RunFile"/> et <see cref="DecodeFile"/>.
/// <para>
/// Le fichier est <b>committé</b> : avec les deux corpus humains, il est l'investissement
/// irremplaçable du chantier. Les tentatives successives s'accumulent, elles ne s'écrasent pas —
/// c'est ce qui rendra lisible, dans six mois, l'effet du passage de <c>decode-clue</c> v1 à v2.
/// </para>
/// </summary>
public static class CalibrationFile
{
    public const int HarnessVersion = 1;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static string PathFor(string directory, string calibrationId) =>
        Path.Combine(directory, $"calibration.{calibrationId}.jsonl");

    /// <summary>Le rapport frère : même nom, extension <c>.json</c>.</summary>
    public static string ReportPathFor(string jsonlPath) =>
        Path.ChangeExtension(jsonlPath, ".json");

    public static void WriteManifest(string path, CalibrationManifest manifest)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, EvalJson.Serialize(manifest) + "\n", Utf8NoBom);
    }

    public static void AppendDecode(string path, CalibrationDecode decode) =>
        File.AppendAllText(path, EvalJson.Serialize(decode) + "\n", Utf8NoBom);

    public static CalibrationContents? ReadOrNull(string path) =>
        File.Exists(path) ? Read(path) : null;

    public static CalibrationContents Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Fichier de calibration introuvable : {path}", path);

        var lines = File.ReadAllLines(path).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0)
            throw new RunIntegrityException($"Fichier de calibration vide : {path}");

        CalibrationManifest manifest;
        try
        {
            manifest = EvalJson.Deserialize<CalibrationManifest>(lines[0]);
        }
        catch (JsonException ex)
        {
            throw new RunIntegrityException($"Manifeste de calibration illisible dans {path} : {ex.Message}");
        }

        if (manifest.Kind != "manifest")
            throw new RunIntegrityException(
                $"La première ligne de {path} n'est pas un manifeste (kind={manifest.Kind}).");

        if (manifest.HarnessVersion != HarnessVersion)
            throw new RunIntegrityException(
                $"{path} : harnessVersion {manifest.HarnessVersion} incompatible avec {HarnessVersion}.");

        var decodes = new List<CalibrationDecode>();
        for (var i = 1; i < lines.Count; i++)
        {
            try
            {
                using var doc = JsonDocument.Parse(lines[i]);
                var kind = doc.RootElement.GetProperty("kind").GetString();
                if (kind != CalibrationDecode.LineKind)
                    throw new RunIntegrityException(
                        $"{path} ligne {i + 1} : kind inconnu « {kind} ».");

                decodes.Add(EvalJson.Deserialize<CalibrationDecode>(lines[i]));
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException && i == lines.Count - 1)
            {
                Console.Error.WriteLine(
                    $"AVERTISSEMENT : dernière ligne tronquée dans {path}, ignorée (calibration interrompue). " +
                    "La reprise la regénérera.");
            }
        }

        return new CalibrationContents(manifest, decodes.AsReadOnly());
    }
}
```

- [ ] **Step 9 : Exécuter, vérifier le passage**

Run : `dotnet test --filter "FullyQualifiedName~CalibrationFileTests"`
Attendu : **11 tests PASS**.

- [ ] **Step 10 : Suite complète et build**

Run : `dotnet build` puis `dotnet test`
Attendu : build propre, suite entière verte.

- [ ] **Step 11 : Commit**

```bash
git add SoClover.Eval/Calibration/CalibrationModel.cs SoClover.Eval/Calibration/DecoderFingerprint.cs SoClover.Eval/Io/CalibrationFile.cs SoClover.Tests/Eval/DecoderFingerprintTests.cs SoClover.Tests/Eval/CalibrationFileTests.cs
git commit -m "feat(eval): empreinte de decodeur et persistance de calibration"
```

---

## Task 4 : `Calibration/CalibrationSet.cs` — constitution du lot

De `comparisons.dev.jsonl` vers deux choses : les **couples retenus** (avec le verdict humain) et les **indices distincts à décoder**. Trois pièges y sont désamorcés : le doublon inversé qui compterait deux fois, l'ancre qui gonflerait l'accord, et l'indice décodé deux fois parce qu'il apparaît dans deux couples.

**Files:**
- Create: `SoClover.Eval/Calibration/CalibrationSet.cs`
- Modify: `SoClover.Tests/Eval/Helpers/HumanTestData.cs` (fabrique `Comparison`)
- Test: `SoClover.Tests/Eval/CalibrationSetTests.cs`

**Interfaces:**
- Consumes:
  - `SoClover.Eval.Human.ComparisonContents`, `ComparisonLine`, `ComparisonOption`
  - `SoClover.Eval.Human.ComparisonFamilies` — `HumanVsModel`, `ModelVsModel`, `HumanVsAssisted`, `Anchor`
  - `SoClover.Eval.Human.ComparisonSources` — `Human`, `Assisted`, `Model`, `Random`
  - `SoClover.Eval.Human.JudgeSession` — `VerdictA = "A"`, `VerdictB = "B"`, `VerdictTie = "tie"`
  - `SoClover.Eval.Io.HumanFile.LatestByComparisonId(ComparisonContents)`
  - `SoClover.Eval.Calibration.CalibrationContents`, `CalibrationDecode` (T3)
- Produces:
  - `SoClover.Eval.Calibration.CalibrationCouple(string ComparisonId, string Family, string BoardId, string Direction, string SourceA, string SourceB, string ClueA, string ClueB, string HumanVerdict)`
  - `CalibrationClue(string BoardId, string Direction, string Clue)`
  - `CalibrationLot(IReadOnlyList<CalibrationCouple> Couples, IReadOnlyList<CalibrationCouple> Anchors, IReadOnlyList<CalibrationClue> Clues)`
  - `CalibrationSet.Build(ComparisonContents contents)` → `CalibrationLot`
  - `CalibrationSet.RBarByClue(CalibrationContents decodes)` → `IReadOnlyDictionary<CalibrationClue, double?>`
  - `CalibrationSet.MainFamilies` → `IReadOnlyList<string>` (`humanVsModel`, `modelVsModel`, `humanVsAssisted`)

- [ ] **Step 1 : Ajouter la fabrique de comparaisons aux helpers**

Dans `SoClover.Tests/Eval/Helpers/HumanTestData.cs`, ajouter à la fin de la classe `HumanTestData` (avant la classe imbriquée `BenchBoardMapperProbe`) :

```csharp
    /// <summary>
    /// Une ligne de comparaison synthétique. <paramref name="duplicateOf"/> reproduit le doublon
    /// inversé de la séance B : même couple, identifiant suffixé, ordre de présentation inversé.
    /// </summary>
    public static ComparisonLine Comparison(
        string comparisonId,
        string verdict,
        string family = ComparisonFamilies.HumanVsModel,
        string boardId = "dev-000",
        string direction = "Top",
        string sourceA = ComparisonSources.Human,
        string sourceB = ComparisonSources.Model,
        string clueA = "indice-humain",
        string clueB = "indice-modele",
        string presentedOrder = PresentedOrders.Ab,
        string? duplicateOf = null,
        int itemOrdinal = 1) => new(
        Kind: "comparison",
        ComparisonId: comparisonId,
        Family: family,
        BoardId: boardId,
        Direction: direction,
        ReferenceWords: ["ref1", "ref2"],
        OptionA: new ComparisonOption(sourceA, sourceA == ComparisonSources.Human ? null : "run-a", clueA),
        OptionB: new ComparisonOption(sourceB, sourceB == ComparisonSources.Human ? null : "run-b", clueB),
        PresentedOrder: presentedOrder,
        Verdict: verdict,
        ElapsedMs: 4200,
        DuplicateOf: duplicateOf,
        SessionId: "s-fixture",
        ItemOrdinal: itemOrdinal,
        JudgedAtUtc: new DateTime(2026, 7, 31, 9, 0, 0, DateTimeKind.Utc).AddMinutes(itemOrdinal));

    public static ComparisonContents Comparisons(
        params ComparisonLine[] lines) =>
        new(ComparisonManifest("aaaaaaaaaaaa", lines.Length), lines.ToList().AsReadOnly());
```

- [ ] **Step 2 : Écrire le test qui échoue**

Créer `SoClover.Tests/Eval/CalibrationSetTests.cs` :

```csharp
using SoClover.Eval.Calibration;
using SoClover.Eval.Human;
using SoClover.Tests.Eval.Helpers;
using Xunit;

namespace SoClover.Tests.Eval;

public class CalibrationSetTests
{
    [Fact]
    public void Keeps_one_couple_per_comparison_with_its_human_verdict()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA),
            HumanTestData.Comparison("c-002", JudgeSession.VerdictB, boardId: "dev-001", itemOrdinal: 2));

        var lot = CalibrationSet.Build(contents);

        Assert.Equal(2, lot.Couples.Count);
        Assert.Equal(JudgeSession.VerdictA, lot.Couples[0].HumanVerdict);
        Assert.Equal(JudgeSession.VerdictB, lot.Couples[1].HumanVerdict);
    }

    // Le doublon inversé désigne le MÊME couple. Le compter deux fois doublerait son poids
    // dans l'accord et dans κ.
    [Fact]
    public void An_inverted_duplicate_counts_once_and_the_last_verdict_wins()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, itemOrdinal: 1),
            HumanTestData.Comparison(
                "c-001-r", JudgeSession.VerdictB, presentedOrder: PresentedOrders.Ba,
                duplicateOf: "c-001", itemOrdinal: 20));

        var lot = CalibrationSet.Build(contents);

        Assert.Single(lot.Couples);
        Assert.Equal(JudgeSession.VerdictB, lot.Couples[0].HumanVerdict);
        Assert.Equal("c-001", lot.Couples[0].ComparisonId);
    }

    // Un re-jugement AJOUTE une ligne sous le même comparisonId : c'est le lecteur qui retient
    // la dernière — règle déjà en place dans HumanFile.
    [Fact]
    public void A_rejudged_comparison_keeps_only_its_last_verdict()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, itemOrdinal: 1),
            HumanTestData.Comparison("c-001", JudgeSession.VerdictTie, itemOrdinal: 2));

        var lot = CalibrationSet.Build(contents);

        Assert.Single(lot.Couples);
        Assert.Equal(JudgeSession.VerdictTie, lot.Couples[0].HumanVerdict);
    }

    // Les ancres restent un contrôle de bon sens du décodeur, pas une mesure : leur écart de
    // qualité est évident et gonflerait artificiellement l'accord et κ.
    [Fact]
    public void Anchors_are_kept_apart_from_the_main_families()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA),
            HumanTestData.Comparison(
                "c-a01", JudgeSession.VerdictA, family: ComparisonFamilies.Anchor,
                sourceA: ComparisonSources.Model, sourceB: ComparisonSources.Random,
                clueA: "indice-modele", clueB: "indice-aleatoire", boardId: "dev-002", itemOrdinal: 2));

        var lot = CalibrationSet.Build(contents);

        Assert.Single(lot.Couples);
        Assert.Single(lot.Anchors);
        Assert.Equal(ComparisonFamilies.Anchor, lot.Anchors[0].Family);
        Assert.DoesNotContain(lot.Couples, c => c.Family == ComparisonFamilies.Anchor);
    }

    [Fact]
    public void The_three_main_families_all_enter_the_main_lot()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, family: ComparisonFamilies.HumanVsModel),
            HumanTestData.Comparison(
                "c-002", JudgeSession.VerdictA, family: ComparisonFamilies.ModelVsModel,
                sourceA: ComparisonSources.Model, boardId: "dev-001", itemOrdinal: 2),
            HumanTestData.Comparison(
                "c-003", JudgeSession.VerdictA, family: ComparisonFamilies.HumanVsAssisted,
                sourceB: ComparisonSources.Assisted, boardId: "dev-002", itemOrdinal: 3));

        var lot = CalibrationSet.Build(contents);

        Assert.Equal(3, lot.Couples.Count);
        Assert.Equal(CalibrationSet.MainFamilies.Order().ToList(),
            lot.Couples.Select(c => c.Family).Distinct().Order().ToList());
    }

    // Le lot d'indices porte les DEUX options de chaque couple, ancres comprises : le décodeur
    // doit évaluer tout ce qui sera comparé.
    [Fact]
    public void The_clue_lot_covers_both_options_of_every_couple_including_anchors()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, clueA: "Alpha", clueB: "Beta"),
            HumanTestData.Comparison(
                "c-a01", JudgeSession.VerdictA, family: ComparisonFamilies.Anchor,
                sourceA: ComparisonSources.Model, sourceB: ComparisonSources.Random,
                clueA: "Gamma", clueB: "Delta", boardId: "dev-002", itemOrdinal: 2));

        var clues = CalibrationSet.Build(contents).Clues.Select(c => c.Clue).ToList();

        Assert.Equal(4, clues.Count);
        Assert.Contains("Alpha", clues);
        Assert.Contains("Delta", clues);
    }

    // ~180 indices distincts × 5 décodages ≈ 900 appels : un indice décodé deux fois, c'est
    // du temps de LM Studio jeté, et deux R̄ différents pour le même indice.
    [Fact]
    public void A_clue_shared_by_two_couples_is_decoded_once()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, clueA: "Humain", clueB: "ModeleA"),
            HumanTestData.Comparison(
                "c-002", JudgeSession.VerdictB, family: ComparisonFamilies.ModelVsModel,
                sourceA: ComparisonSources.Model, clueA: "ModeleA", clueB: "ModeleB", itemOrdinal: 2));

        var clues = CalibrationSet.Build(contents).Clues;

        Assert.Equal(3, clues.Count);
        Assert.Single(clues.Where(c => c.Clue == "ModeleA"));
    }

    // Le même mot sur deux directions différentes reste deux indices : la paire de référence
    // n'est pas la même, donc R non plus.
    [Fact]
    public void The_same_word_on_two_directions_is_two_distinct_clues()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, direction: "Top", clueA: "Mer"),
            HumanTestData.Comparison("c-002", JudgeSession.VerdictA, direction: "Left", clueA: "Mer", itemOrdinal: 2));

        var clues = CalibrationSet.Build(contents).Clues.Where(c => c.Clue == "Mer").ToList();

        Assert.Equal(2, clues.Count);
    }

    [Fact]
    public void A_couple_whose_two_clues_are_identical_is_dropped()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-001", JudgeSession.VerdictTie, clueA: "Même", clueB: "Même"));

        var lot = CalibrationSet.Build(contents);

        Assert.Empty(lot.Couples);
        Assert.Empty(lot.Clues);
    }

    [Fact]
    public void The_lot_is_ordered_deterministically()
    {
        var contents = HumanTestData.Comparisons(
            HumanTestData.Comparison("c-002", JudgeSession.VerdictA, boardId: "dev-005", direction: "Left", itemOrdinal: 1),
            HumanTestData.Comparison("c-001", JudgeSession.VerdictA, boardId: "dev-001", direction: "Top", itemOrdinal: 2));

        var first = CalibrationSet.Build(contents).Clues.Select(c => (c.BoardId, c.Direction, c.Clue)).ToList();
        var second = CalibrationSet.Build(contents).Clues.Select(c => (c.BoardId, c.Direction, c.Clue)).ToList();

        Assert.Equal(first, second);
        Assert.Equal("dev-001", first[0].BoardId);
    }

    // ---- R̄ par indice --------------------------------------------------------

    [Fact]
    public void RBar_averages_the_scored_decodes_of_a_clue()
    {
        var decodes = Decodes(("Pédiatre", 1.0), ("Pédiatre", 0.5), ("Pédiatre", 0.0));

        var rbar = CalibrationSet.RBarByClue(decodes);

        Assert.Equal(0.5, rbar[new CalibrationClue("dev-007", "Top", "Pédiatre")]!.Value, precision: 10);
    }

    // Invariant déjà tenu par RunMetrics, et pour la même raison : un décodeur qui ne sait pas
    // répondre au format n'est pas un décodeur qui se trompe.
    [Fact]
    public void A_format_failure_leaves_the_denominator_rather_than_counting_zero()
    {
        var decodes = Decodes(("Pédiatre", 1.0), ("Pédiatre", null), ("Pédiatre", 1.0));

        var rbar = CalibrationSet.RBarByClue(decodes);

        Assert.Equal(1.0, rbar[new CalibrationClue("dev-007", "Top", "Pédiatre")]!.Value, precision: 10);
    }

    [Fact]
    public void A_clue_without_a_single_scored_decode_has_no_RBar()
    {
        var decodes = Decodes(("Pédiatre", null), ("Pédiatre", null));

        var rbar = CalibrationSet.RBarByClue(decodes);

        Assert.Null(rbar[new CalibrationClue("dev-007", "Top", "Pédiatre")]);
    }

    private static CalibrationContents Decodes(params (string Clue, double? R)[] entries)
    {
        var lines = entries.Select((e, i) => new CalibrationDecode(
            Kind: CalibrationDecode.LineKind,
            BoardId: "dev-007",
            Direction: "Top",
            Clue: e.Clue,
            DecodeIndex: i,
            Picked: e.R is null ? null : ["Chirurgien", "Enfant"],
            R: e.R,
            ShuffleSeed: "1",
            DecodeFailureKind: e.R is null ? "unparseable" : null,
            LatencyMs: 100)).ToList();

        return new CalibrationContents(
            CalibrationFileTestsManifest(), lines.AsReadOnly());
    }

    private static CalibrationManifest CalibrationFileTestsManifest() => new(
        Kind: "manifest", CalibrationId: "20260805-3f2a91c4e0d1",
        CreatedAtUtc: new DateTime(2026, 8, 5, 9, 0, 0, DateTimeKind.Utc),
        ComparisonsFile: "eval/human/comparisons.dev.jsonl", BenchFile: "eval/boards.dev.jsonl",
        BenchHash: "aaaaaaaaaaaa", CoupleCount: 0, ClueCount: 0,
        DecoderFingerprint: "3f2a91c4e0d1", Provider: "OpenAI",
        BaseUrl: "http://localhost:1234/v1", ModelId: "m", ModelSnapshotDate: null,
        ProviderModelListHash: null, Temperature: 0.3, TopP: null, MaxOutputTokens: 512,
        CluePromptFile: "fr/decode-clue.md", CluePromptVersion: 2, DecodesPerClue: 5,
        Epsilon: 0.0, HarnessVersion: SoClover.Eval.Io.CalibrationFile.HarnessVersion,
        OperatorNotes: null);
}
```

- [ ] **Step 3 : Exécuter, vérifier l'échec**

Run : `dotnet test --filter "FullyQualifiedName~CalibrationSetTests"`
Attendu : **échec de compilation** — `CalibrationSet`, `CalibrationCouple`, `CalibrationClue`, `CalibrationLot` n'existent pas.

- [ ] **Step 4 : Écrire `Calibration/CalibrationSet.cs`**

```csharp
using SoClover.Eval.Human;
using SoClover.Eval.Io;

namespace SoClover.Eval.Calibration;

/// <summary>Un indice à décoder, identifié par sa direction — la paire de référence en dépend.</summary>
public sealed record CalibrationClue(string BoardId, string Direction, string Clue);

/// <summary>
/// Un couple à trancher par le décodeur. <see cref="SourceA"/>/<see cref="SourceB"/> sont conservés
/// parce que le verdict d'une <b>ancre</b> ne se lit qu'à travers eux : le décodeur a raison quand
/// il préfère l'indice réel à l'aléatoire.
/// </summary>
public sealed record CalibrationCouple(
    string ComparisonId,
    string Family,
    string BoardId,
    string Direction,
    string SourceA,
    string SourceB,
    string ClueA,
    string ClueB,
    string HumanVerdict);

public sealed record CalibrationLot(
    IReadOnlyList<CalibrationCouple> Couples,
    IReadOnlyList<CalibrationCouple> Anchors,
    IReadOnlyList<CalibrationClue> Clues);

/// <summary>
/// De <c>comparisons.dev.jsonl</c> vers le lot de calibration : couples retenus et indices
/// distincts à décoder.
/// <para>
/// Trois pièges y sont désamorcés une bonne fois : un <b>doublon inversé</b> désigne le même
/// couple et ne le compte qu'une fois (dernier verdict retenu — « la dernière ligne gagne », la
/// règle de lecture déjà en place dans <see cref="HumanFile"/>) ; les <b>ancres</b> sortent du
/// calcul principal ; et un indice partagé par deux couples n'est <b>décodé qu'une fois</b>.
/// </para>
/// </summary>
public static class CalibrationSet
{
    /// <summary>
    /// Les familles qui entrent dans le calcul principal. <c>anchor</c> en est exclue et
    /// rapportée à part : y inclure les ancres reviendrait à mesurer l'accord sur des couples
    /// dont l'écart de qualité est évident — exactement ce que le design P4-P5 refusait déjà de
    /// faire côté humain.
    /// </summary>
    public static readonly IReadOnlyList<string> MainFamilies =
    [
        ComparisonFamilies.HumanVsModel,
        ComparisonFamilies.ModelVsModel,
        ComparisonFamilies.HumanVsAssisted,
    ];

    public static CalibrationLot Build(ComparisonContents contents)
    {
        // Un re-jugement ajoute une ligne sous le même comparisonId : seul le dernier compte.
        var latest = HumanFile.LatestByComparisonId(contents);

        // Puis le doublon inversé : il porte « <original>-r » et DuplicateOf = « <original> ».
        // Les deux lignes parlent du même couple — on les replie sur la clé de l'original, en
        // gardant la dernière apparition dans le fichier.
        var byCouple = new Dictionary<string, ComparisonLine>(StringComparer.Ordinal);
        foreach (var line in contents.Comparisons)
        {
            if (!latest.TryGetValue(line.ComparisonId, out var kept) || !ReferenceEquals(kept, line))
                continue;

            byCouple[line.DuplicateOf ?? line.ComparisonId] = line;
        }

        var couples = new List<CalibrationCouple>();
        var anchors = new List<CalibrationCouple>();

        foreach (var (coupleId, line) in byCouple
                     .OrderBy(kv => kv.Value.BoardId, StringComparer.Ordinal)
                     .ThenBy(kv => kv.Value.Direction, StringComparer.Ordinal)
                     .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                     .Select(kv => (kv.Key, kv.Value)))
        {
            // Rien à trancher : deux indices identiques ne portent aucune préférence, et le
            // décodeur leur donnerait mécaniquement le même R̄.
            if (string.Equals(line.OptionA.Clue.Trim(), line.OptionB.Clue.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                continue;

            var couple = new CalibrationCouple(
                ComparisonId: coupleId,
                Family: line.Family,
                BoardId: line.BoardId,
                Direction: line.Direction,
                SourceA: line.OptionA.Source,
                SourceB: line.OptionB.Source,
                ClueA: line.OptionA.Clue,
                ClueB: line.OptionB.Clue,
                HumanVerdict: line.Verdict);

            if (line.Family == ComparisonFamilies.Anchor)
                anchors.Add(couple);
            else
                couples.Add(couple);
        }

        var clues = couples.Concat(anchors)
            .SelectMany(c => new[]
            {
                new CalibrationClue(c.BoardId, c.Direction, c.ClueA),
                new CalibrationClue(c.BoardId, c.Direction, c.ClueB),
            })
            .Distinct()
            .OrderBy(c => c.BoardId, StringComparer.Ordinal)
            .ThenBy(c => c.Direction, StringComparer.Ordinal)
            .ThenBy(c => c.Clue, StringComparer.Ordinal)
            .ToList();

        return new CalibrationLot(couples.AsReadOnly(), anchors.AsReadOnly(), clues.AsReadOnly());
    }

    /// <summary>
    /// R̄ par indice, calculé sur les décodages <b>valides</b>. Un décodage en échec de format est
    /// <b>exclu du dénominateur</b>, jamais compté 0 — invariant déjà tenu par <c>RunMetrics</c>,
    /// et pour la même raison : un décodeur qui ne sait pas répondre au format n'est pas un
    /// décodeur qui se trompe.
    /// <para>
    /// <c>null</c> quand aucun décodage n'est exploitable : R̄ est alors <b>indéfini</b>, et le
    /// couple concerné sortira du calcul plutôt que d'être compté <c>tie</c> ou perdant.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<CalibrationClue, double?> RBarByClue(CalibrationContents decodes) =>
        decodes.Decodes
            .GroupBy(d => new CalibrationClue(d.BoardId, d.Direction, d.Clue))
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var scored = g.Where(d => d.DecodeFailureKind is null && d.R is not null).ToList();
                    return scored.Count == 0 ? (double?)null : scored.Average(d => d.R!.Value);
                });
}
```

- [ ] **Step 5 : Exécuter, vérifier le passage**

Run : `dotnet test --filter "FullyQualifiedName~CalibrationSetTests"`
Attendu : **13 tests PASS**.

- [ ] **Step 6 : Suite complète et build**

Run : `dotnet build` puis `dotnet test`
Attendu : build propre, suite entière verte — `HumanFileTests`, `HumanReportTests` et `ComparisonPlanTests` inclus (le helper est partagé, ses ajouts sont purement additifs).

- [ ] **Step 7 : Commit**

```bash
git add SoClover.Eval/Calibration/CalibrationSet.cs SoClover.Tests/Eval/CalibrationSetTests.cs SoClover.Tests/Eval/Helpers/HumanTestData.cs
git commit -m "feat(eval): constitution du lot de calibration — doublons replies, ancres a part"
```

---

## Task 5 : `Calibration/AgreementMetrics.cs` — accord, κ, contingence, IC

Le cœur statistique. Deux invariants s'y jouent, et ils ne se voient pas à l'exécution : **κ se calcule sur les slots canoniques, jamais sur les positions** (sinon il mesure le biais de position), et **les égalités sortent du dénominateur** — ce qui, avec `half_rate = 0,656`, peut réduire l'accord à trente comparaisons sans que rien ne le signale.

**Files:**
- Create: `SoClover.Eval/Calibration/AgreementMetrics.cs`
- Test: `SoClover.Tests/Eval/AgreementMetricsTests.cs`

**Interfaces:**
- Consumes:
  - `SoClover.Eval.Calibration.CalibrationLot`, `CalibrationCouple`, `CalibrationClue` (T4)
  - `SoClover.Eval.Scoring.Bootstrap.Ci<T>` et `Bootstrap.DefaultIterations` (T2)
  - `SoClover.Eval.Human.JudgeSession.VerdictA/VerdictB/VerdictTie`
  - `SoClover.Eval.Human.ComparisonSources.Model`
- Produces:
  - `AgreementMetrics.DefaultEpsilon` → `const double = 0.0`
  - `AgreementMetrics.DefaultBootstrapSeed` → `const long = 20260805001`
  - `AgreementMetrics.ThinDenominatorThreshold` → `const int = 40`
  - `AgreementMetrics.DecoderVerdict(double? rBarA, double? rBarB, double epsilon)` → `string?` (`null` = indécidable)
  - `AgreementMetrics.KappaOf(IReadOnlyList<VerdictPair> pairs)` → `double` (`internal`)
  - `AgreementMetrics.AgreementOf(IReadOnlyList<VerdictPair> pairs)` → `double` (`internal`)
  - `AgreementMetrics.Compute(CalibrationLot lot, IReadOnlyDictionary<CalibrationClue, double?> rBar, double epsilon, int bootstrapIterations, long seed)` → `AgreementReport`
  - records `VerdictPair`, `ContingencyCell`, `FamilyAgreement`, `AgreementReport`

- [ ] **Step 1 : Écrire le test qui échoue**

Créer `SoClover.Tests/Eval/AgreementMetricsTests.cs` :

```csharp
using SoClover.Eval.Calibration;
using SoClover.Eval.Human;
using Xunit;

namespace SoClover.Tests.Eval;

public class AgreementMetricsTests
{
    // ---- Verdict du décodeur --------------------------------------------------

    [Theory]
    [InlineData(1.0, 0.5, JudgeSession.VerdictA)]
    [InlineData(0.5, 1.0, JudgeSession.VerdictB)]
    [InlineData(0.5, 0.5, JudgeSession.VerdictTie)]
    public void The_decoder_verdict_follows_the_sign_of_the_RBar_gap(double a, double b, string expected)
    {
        Assert.Equal(expected, AgreementMetrics.DecoderVerdict(a, b, epsilon: 0.0));
    }

    // ε n'est PAS un réglage libre : le modifier après avoir vu l'accord serait ajuster
    // l'instrument sur sa propre mesure. Il est donc explicite, et consigné au manifeste.
    [Fact]
    public void A_gap_below_epsilon_is_a_tie()
    {
        Assert.Equal(JudgeSession.VerdictTie, AgreementMetrics.DecoderVerdict(0.6, 0.5, epsilon: 0.2));
        Assert.Equal(JudgeSession.VerdictA, AgreementMetrics.DecoderVerdict(0.8, 0.5, epsilon: 0.2));
    }

    [Fact]
    public void An_undefined_RBar_makes_the_verdict_undecidable()
    {
        Assert.Null(AgreementMetrics.DecoderVerdict(null, 0.5, epsilon: 0.0));
        Assert.Null(AgreementMetrics.DecoderVerdict(0.5, null, epsilon: 0.0));
    }

    // ---- Accord ---------------------------------------------------------------

    [Fact]
    public void Agreement_is_one_on_a_fully_concordant_corpus()
    {
        var report = Compute(
            Couple("c-1", JudgeSession.VerdictA, 1.0, 0.0),
            Couple("c-2", JudgeSession.VerdictB, 0.0, 1.0),
            Couple("c-3", JudgeSession.VerdictA, 1.0, 0.5));

        Assert.Equal(3, report.DecidedCount);
        Assert.Equal(1.0, report.Agreement, precision: 10);
    }

    // LE risque du dénominateur : avec half_rate = 0,656, une large fraction des couples aura
    // R̄_A = R̄_B = 0,5. Le décodeur dit « tie », le couple sort du dénominateur, et la porte
    // finit par se jouer sur trente comparaisons.
    [Fact]
    public void Ties_on_either_side_leave_the_denominator()
    {
        var report = Compute(
            Couple("c-1", JudgeSession.VerdictA, 1.0, 0.0),
            Couple("c-2", JudgeSession.VerdictTie, 1.0, 0.0),
            Couple("c-3", JudgeSession.VerdictA, 0.5, 0.5));

        Assert.Equal(3, report.CoupleCount);
        Assert.Equal(1, report.DecidedCount);
        Assert.Equal(1.0, report.Agreement, precision: 10);
    }

    [Fact]
    public void Both_tie_rates_are_published_next_to_the_agreement()
    {
        var report = Compute(
            Couple("c-1", JudgeSession.VerdictA, 1.0, 0.0),
            Couple("c-2", JudgeSession.VerdictTie, 1.0, 0.0),
            Couple("c-3", JudgeSession.VerdictA, 0.5, 0.5),
            Couple("c-4", JudgeSession.VerdictB, 0.0, 1.0));

        Assert.Equal(0.25, report.HumanTieRate, precision: 10);
        Assert.Equal(0.25, report.DecoderTieRate, precision: 10);
    }

    // Un accord de 0,80 sur 28 couples a un IC qui traverse la porte, et le rapport doit le dire.
    [Fact]
    public void A_denominator_below_forty_couples_is_flagged_fragile()
    {
        var couples = Enumerable.Range(0, 39)
            .Select(i => Couple($"c-{i}", JudgeSession.VerdictA, 1.0, 0.0))
            .ToArray();

        Assert.True(Compute(couples).ThinDenominator);
    }

    [Fact]
    public void A_denominator_of_forty_couples_is_not_flagged()
    {
        var couples = Enumerable.Range(0, 40)
            .Select(i => Couple($"c-{i}", JudgeSession.VerdictA, 1.0, 0.0))
            .ToArray();

        Assert.False(Compute(couples).ThinDenominator);
    }

    // D3 : R̄ indéfini d'un côté ne se compare pas. Le compter « tie » gonflerait le taux
    // d'égalité du décodeur ; le compter perdant serait faux.
    [Fact]
    public void A_couple_whose_clue_has_no_scored_decode_is_excluded_and_counted()
    {
        var report = Compute(
            Couple("c-1", JudgeSession.VerdictA, 1.0, 0.0),
            Couple("c-2", JudgeSession.VerdictA, null, 0.0));

        Assert.Equal(1, report.UnscorableCoupleCount);
        Assert.Equal(1, report.DecidedCount);
    }

    // ---- Cohen's κ ------------------------------------------------------------

    [Fact]
    public void Kappa_is_one_on_perfect_agreement_with_balanced_marginals()
    {
        var couples = Enumerable.Range(0, 40)
            .Select(i => i % 2 == 0
                ? Couple($"c-{i}", JudgeSession.VerdictA, 1.0, 0.0)
                : Couple($"c-{i}", JudgeSession.VerdictB, 0.0, 1.0))
            .ToArray();

        Assert.Equal(1.0, Compute(couples).Kappa, precision: 10);
    }

    [Fact]
    public void Kappa_is_near_zero_when_the_two_judges_are_independent()
    {
        // Humain alterne A/B ; décodeur alterne selon un cycle de 2 décalé d'un item sur deux :
        // accord observé 0,5, marginales équilibrées, donc p_e = 0,5 et κ = 0.
        var couples = Enumerable.Range(0, 40)
            .Select(i => Couple(
                $"c-{i}",
                i % 2 == 0 ? JudgeSession.VerdictA : JudgeSession.VerdictB,
                (i % 4) is 0 or 1 ? 1.0 : 0.0,
                (i % 4) is 0 or 1 ? 0.0 : 1.0))
            .ToArray();

        Assert.Equal(0.0, Compute(couples).Kappa, precision: 6);
    }

    // LE PARADOXE, documenté avant d'être rencontré : sur humanVsModel, l'humain gagnera
    // probablement la grande majorité des couples. p_e s'approche de p_o et κ s'effondre
    // MALGRÉ un accord élevé. Ce n'est pas un défaut du décodeur.
    [Fact]
    public void Kappa_collapses_on_skewed_marginals_despite_a_high_agreement()
    {
        var couples = new List<CalibrationCouple>();
        var rbar = new Dictionary<CalibrationClue, double?>();

        for (var i = 0; i < 100; i++)
        {
            // 90 % de victoires humaines des deux côtés ; les 10 % restants se répartissent
            // de sorte que l'accord observé vaille 0,90.
            var human = i < 90 ? JudgeSession.VerdictA : JudgeSession.VerdictB;
            var decoder = i < 85 || i >= 95 ? JudgeSession.VerdictA : JudgeSession.VerdictB;
            AddCouple(couples, rbar, $"c-{i}", human,
                decoder == JudgeSession.VerdictA ? 1.0 : 0.0,
                decoder == JudgeSession.VerdictA ? 0.0 : 1.0);
        }

        var report = AgreementMetrics.Compute(
            new CalibrationLot(couples.AsReadOnly(), [], []), rbar,
            epsilon: 0.0, bootstrapIterations: 200, seed: 1);

        Assert.Equal(0.90, report.Agreement, precision: 2);
        Assert.True(report.Kappa < 0.40,
            $"κ devrait s'effondrer sur marginales déséquilibrées, obtenu {report.Kappa}");
        // La table de contingence est la SEULE façon de distinguer « le décodeur est mauvais »
        // de « la statistique est mal conditionnée ».
        Assert.Equal(4, report.Contingency.Count);
        Assert.Equal(0.90, report.HumanMarginalA, precision: 2);
    }

    [Fact]
    public void Kappa_is_zero_rather_than_undefined_when_both_marginals_are_degenerate()
    {
        var couples = Enumerable.Range(0, 40)
            .Select(i => Couple($"c-{i}", JudgeSession.VerdictA, 1.0, 0.0))
            .ToArray();

        Assert.Equal(0.0, Compute(couples).Kappa, precision: 10);
    }

    // L'INVARIANT CENTRAL du design P4-P5, testé explicitement : optionA/optionB sont les slots
    // CANONIQUES, presentedOrder dit seulement lequel fut affiché en position 1. Un κ calculé
    // sur les positions mesurerait le biais de position, pas l'accord.
    [Fact]
    public void Kappa_is_unchanged_when_the_presented_order_is_inverted()
    {
        var couples = Enumerable.Range(0, 40)
            .Select(i => i % 3 == 0
                ? Couple($"c-{i}", JudgeSession.VerdictA, 1.0, 0.0)
                : Couple($"c-{i}", JudgeSession.VerdictB, 0.0, 1.0))
            .ToArray();

        var straight = Compute(couples);
        // Le lot de calibration ne porte AUCUN champ d'ordre de présentation : l'inversion est
        // structurellement sans effet. Ce test est le garde-fou qui casse si quelqu'un
        // réintroduit presentedOrder dans CalibrationCouple.
        var inverted = Compute(couples.Reverse().ToArray());

        Assert.Equal(straight.Kappa, inverted.Kappa, precision: 10);
        Assert.Equal(straight.Agreement, inverted.Agreement, precision: 10);
    }

    // modelVsModel a des marginales naturellement plus équilibrées : c'est la famille la plus
    // informative pour κ, et c'est pour ça que le design P4-P5 lui a réservé ~40 couples.
    [Fact]
    public void Kappa_is_also_reported_per_family()
    {
        var couples = Enumerable.Range(0, 20)
            .Select(i => Couple($"h-{i}", JudgeSession.VerdictA, 1.0, 0.0,
                family: ComparisonFamilies.HumanVsModel))
            .Concat(Enumerable.Range(0, 20)
                .Select(i => Couple($"m-{i}", i % 2 == 0 ? JudgeSession.VerdictA : JudgeSession.VerdictB,
                    i % 2 == 0 ? 1.0 : 0.0, i % 2 == 0 ? 0.0 : 1.0,
                    family: ComparisonFamilies.ModelVsModel)))
            .ToArray();

        var report = Compute(couples);
        var modelVsModel = report.ByFamily.Single(f => f.Family == ComparisonFamilies.ModelVsModel);

        Assert.Equal(2, report.ByFamily.Count);
        Assert.Equal(20, modelVsModel.DecidedCount);
        Assert.Equal(1.0, modelVsModel.Kappa, precision: 10);
    }

    // Le PRD nomme κ ; la porte reste κ. PABAK est un DIAGNOSTIC — le publier comme porte
    // serait changer la règle en cours de partie.
    [Fact]
    public void Pabak_is_reported_as_a_diagnostic()
    {
        var couples = Enumerable.Range(0, 40)
            .Select(i => i < 36
                ? Couple($"c-{i}", JudgeSession.VerdictA, 1.0, 0.0)
                : Couple($"c-{i}", JudgeSession.VerdictA, 0.0, 1.0))
            .ToArray();

        var report = Compute(couples);

        Assert.Equal(2 * report.Agreement - 1, report.Pabak, precision: 10);
    }

    // ---- IC bootstrap ---------------------------------------------------------

    // Une porte à 0,75 franchie à 0,76 avec un IC [0,61 ; 0,88] doit être VISIBLEMENT fragile,
    // pas discrètement franchie.
    [Fact]
    public void The_agreement_confidence_interval_brackets_the_agreement()
    {
        var couples = Enumerable.Range(0, 60)
            .Select(i => Couple($"c-{i}", i % 5 == 0 ? JudgeSession.VerdictB : JudgeSession.VerdictA, 1.0, 0.0))
            .ToArray();

        var report = Compute(couples);

        Assert.True(report.AgreementCiLow <= report.Agreement);
        Assert.True(report.Agreement <= report.AgreementCiHigh);
        Assert.True(report.KappaCiLow <= report.Kappa);
        Assert.True(report.Kappa <= report.KappaCiHigh);
    }

    [Fact]
    public void The_confidence_intervals_are_deterministic_for_a_fixed_seed()
    {
        var couples = Enumerable.Range(0, 60)
            .Select(i => Couple($"c-{i}", i % 5 == 0 ? JudgeSession.VerdictB : JudgeSession.VerdictA, 1.0, 0.0))
            .ToArray();

        var a = Compute(couples);
        var b = Compute(couples);

        Assert.Equal(a.AgreementCiLow, b.AgreementCiLow);
        Assert.Equal(a.KappaCiHigh, b.KappaCiHigh);
    }

    // ---- Ancres ---------------------------------------------------------------

    // Attendu : le décodeur préfère l'indice réel à l'aléatoire sur ≥ 4 des 5 ancres.
    // Contrôle de bon sens, jamais une mesure.
    [Fact]
    public void Anchors_are_scored_apart_on_whether_the_decoder_prefers_the_real_clue()
    {
        var couples = new List<CalibrationCouple>();
        var anchors = new List<CalibrationCouple>();
        var rbar = new Dictionary<CalibrationClue, double?>();

        AddCouple(couples, rbar, "c-1", JudgeSession.VerdictA, 1.0, 0.0);
        for (var i = 0; i < 5; i++)
        {
            // Source A = modèle, source B = aléatoire. Le décodeur a raison quand il dit « A ».
            AddCouple(anchors, rbar, $"a-{i}", JudgeSession.VerdictA,
                i < 4 ? 1.0 : 0.0, i < 4 ? 0.0 : 1.0,
                family: ComparisonFamilies.Anchor,
                sourceA: ComparisonSources.Model, sourceB: ComparisonSources.Random);
        }

        var report = AgreementMetrics.Compute(
            new CalibrationLot(couples.AsReadOnly(), anchors.AsReadOnly(), []), rbar,
            epsilon: 0.0, bootstrapIterations: 200, seed: 1);

        Assert.Equal(5, report.AnchorCount);
        Assert.Equal(4, report.AnchorCorrect);
        // Les ancres n'ont PAS gonflé l'accord principal.
        Assert.Equal(1, report.DecidedCount);
    }

    // ---- Fabriques ------------------------------------------------------------

    private static readonly Dictionary<string, (CalibrationCouple Couple, double? A, double? B)> Built = new();

    private static CalibrationCouple Couple(
        string id, string humanVerdict, double? rBarA, double? rBarB,
        string family = ComparisonFamilies.HumanVsModel)
    {
        var couple = new CalibrationCouple(
            ComparisonId: id, Family: family, BoardId: "dev-000", Direction: "Top",
            SourceA: ComparisonSources.Human, SourceB: ComparisonSources.Model,
            ClueA: $"{id}-a", ClueB: $"{id}-b", HumanVerdict: humanVerdict);

        Built[id] = (couple, rBarA, rBarB);
        return couple;
    }

    private static void AddCouple(
        List<CalibrationCouple> into, Dictionary<CalibrationClue, double?> rbar,
        string id, string humanVerdict, double? rBarA, double? rBarB,
        string family = ComparisonFamilies.HumanVsModel,
        string sourceA = ComparisonSources.Human,
        string sourceB = ComparisonSources.Model)
    {
        var couple = new CalibrationCouple(
            ComparisonId: id, Family: family, BoardId: "dev-000", Direction: "Top",
            SourceA: sourceA, SourceB: sourceB,
            ClueA: $"{id}-a", ClueB: $"{id}-b", HumanVerdict: humanVerdict);

        into.Add(couple);
        rbar[new CalibrationClue("dev-000", "Top", $"{id}-a")] = rBarA;
        rbar[new CalibrationClue("dev-000", "Top", $"{id}-b")] = rBarB;
    }

    private static AgreementReport Compute(params CalibrationCouple[] couples)
    {
        var rbar = new Dictionary<CalibrationClue, double?>();
        foreach (var couple in couples)
        {
            var (_, a, b) = Built[couple.ComparisonId];
            rbar[new CalibrationClue(couple.BoardId, couple.Direction, couple.ClueA)] = a;
            rbar[new CalibrationClue(couple.BoardId, couple.Direction, couple.ClueB)] = b;
        }

        return AgreementMetrics.Compute(
            new CalibrationLot(couples.ToList().AsReadOnly(), [], []), rbar,
            epsilon: AgreementMetrics.DefaultEpsilon,
            bootstrapIterations: 200,
            seed: AgreementMetrics.DefaultBootstrapSeed);
    }
}
```

> **Note au relecteur** : `Built` est un dictionnaire statique de fabrique, acceptable ici parce que les clés portent l'identifiant du couple et que chaque test construit les siens. Si un agent le juge fragile, remplacer par la variante explicite `AddCouple` déjà utilisée par les trois derniers tests — les deux mécanismes coexistent volontairement.

- [ ] **Step 2 : Exécuter, vérifier l'échec**

Run : `dotnet test --filter "FullyQualifiedName~AgreementMetricsTests"`
Attendu : **échec de compilation** — `AgreementMetrics` et `AgreementReport` n'existent pas.

- [ ] **Step 3 : Écrire `Calibration/AgreementMetrics.cs`**

```csharp
using SoClover.Eval.Human;
using SoClover.Eval.Scoring;

namespace SoClover.Eval.Calibration;

/// <summary>Un couple doublement tranché : c'est l'unité de l'accord, de κ et du bootstrap.</summary>
public sealed record VerdictPair(string Human, string Decoder);

public sealed record ContingencyCell(string HumanVerdict, string DecoderVerdict, int Count);

public sealed record FamilyAgreement(
    string Family, int CoupleCount, int DecidedCount, double Agreement, double Kappa);

public sealed record AgreementReport(
    int CoupleCount,
    int DecidedCount,
    int UnscorableCoupleCount,
    double Agreement,
    double AgreementCiLow,
    double AgreementCiHigh,
    double Kappa,
    double KappaCiLow,
    double KappaCiHigh,
    double Pabak,
    double HumanTieRate,
    double DecoderTieRate,
    IReadOnlyList<ContingencyCell> Contingency,
    double HumanMarginalA,
    double HumanMarginalB,
    double DecoderMarginalA,
    double DecoderMarginalB,
    IReadOnlyList<FamilyAgreement> ByFamily,
    int AnchorCount,
    int AnchorCorrect,
    bool ThinDenominator);

/// <summary>
/// Accord décodeur/humain et Cohen's κ, sur les <b>slots canoniques</b>.
/// <para>
/// <b>Jamais sur les positions.</b> C'est l'invariant central du design P4-P5 : <c>optionA</c> et
/// <c>optionB</c> sont ordonnés par source de façon déterministe, <c>presentedOrder</c> dit
/// seulement lequel fut affiché en position 1. Un κ calculé sur les positions mesurerait le biais
/// de position, pas l'accord — et aucun test fonctionnel ne s'en apercevrait.
/// <see cref="CalibrationCouple"/> ne porte volontairement aucun champ d'ordre de présentation.
/// </para>
/// </summary>
public static class AgreementMetrics
{
    /// <summary>Tout écart compte. Une valeur non nulle se décide AVANT de lire l'accord.</summary>
    public const double DefaultEpsilon = 0.0;

    public const long DefaultBootstrapSeed = 20260805001;

    /// <summary>Sous ce dénominateur, l'IC traverse la porte : le chiffre est publié marqué fragile.</summary>
    public const int ThinDenominatorThreshold = 40;

    /// <summary>
    /// <c>null</c> quand l'un des deux R̄ est indéfini : R̄ indéfini d'un côté ne se compare pas.
    /// Compter <c>tie</c> gonflerait le taux d'égalité du décodeur ; compter perdant serait faux.
    /// </summary>
    public static string? DecoderVerdict(double? rBarA, double? rBarB, double epsilon)
    {
        if (rBarA is not { } a || rBarB is not { } b)
            return null;

        if (a - b > epsilon) return JudgeSession.VerdictA;
        if (b - a > epsilon) return JudgeSession.VerdictB;
        return JudgeSession.VerdictTie;
    }

    public static AgreementReport Compute(
        CalibrationLot lot,
        IReadOnlyDictionary<CalibrationClue, double?> rBar,
        double epsilon,
        int bootstrapIterations,
        long seed)
    {
        double? Of(CalibrationCouple c, string clue) =>
            rBar.TryGetValue(new CalibrationClue(c.BoardId, c.Direction, clue), out var v) ? v : null;

        var scored = new List<(CalibrationCouple Couple, string Decoder)>();
        var unscorable = 0;

        foreach (var couple in lot.Couples)
        {
            var verdict = DecoderVerdict(Of(couple, couple.ClueA), Of(couple, couple.ClueB), epsilon);
            if (verdict is null) { unscorable++; continue; }
            scored.Add((couple, verdict));
        }

        // L'accord brut est calculé HORS ÉGALITÉS, comme le prescrit le PRD :
        //   accord = #(verdicts identiques) / #(couples où humain ET décodeur tranchent)
        var decided = scored
            .Where(s => s.Couple.HumanVerdict != JudgeSession.VerdictTie
                        && s.Decoder != JudgeSession.VerdictTie)
            .Select(s => new VerdictPair(s.Couple.HumanVerdict, s.Decoder))
            .ToList();

        var agreement = AgreementOf(decided);
        var kappa = KappaOf(decided);

        var (agreementLow, agreementHigh) = decided.Count == 0
            ? (0.0, 0.0)
            : Bootstrap.Ci(decided, AgreementOf, bootstrapIterations, seed);
        var (kappaLow, kappaHigh) = decided.Count == 0
            ? (0.0, 0.0)
            : Bootstrap.Ci(decided, KappaOf, bootstrapIterations, seed);

        var byFamily = scored
            .GroupBy(s => s.Couple.Family, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var pairs = g
                    .Where(s => s.Couple.HumanVerdict != JudgeSession.VerdictTie
                                && s.Decoder != JudgeSession.VerdictTie)
                    .Select(s => new VerdictPair(s.Couple.HumanVerdict, s.Decoder))
                    .ToList();
                return new FamilyAgreement(g.Key, g.Count(), pairs.Count, AgreementOf(pairs), KappaOf(pairs));
            })
            .ToList()
            .AsReadOnly();

        var categories = new[] { JudgeSession.VerdictA, JudgeSession.VerdictB };
        var contingency = categories
            .SelectMany(h => categories.Select(d =>
                new ContingencyCell(h, d, decided.Count(p => p.Human == h && p.Decoder == d))))
            .ToList()
            .AsReadOnly();

        var anchorCorrect = lot.Anchors.Count(a =>
        {
            var verdict = DecoderVerdict(Of(a, a.ClueA), Of(a, a.ClueB), epsilon);
            var winnerSource = verdict switch
            {
                JudgeSession.VerdictA => a.SourceA,
                JudgeSession.VerdictB => a.SourceB,
                _ => null,
            };
            return winnerSource == ComparisonSources.Model;
        });

        return new AgreementReport(
            CoupleCount: lot.Couples.Count,
            DecidedCount: decided.Count,
            UnscorableCoupleCount: unscorable,
            Agreement: agreement,
            AgreementCiLow: agreementLow,
            AgreementCiHigh: agreementHigh,
            Kappa: kappa,
            KappaCiLow: kappaLow,
            KappaCiHigh: kappaHigh,
            // PABAK est un DIAGNOSTIC. Le PRD nomme κ ; la porte reste κ. Publier PABAK comme
            // porte serait changer la règle en cours de partie.
            Pabak: 2 * agreement - 1,
            HumanTieRate: Ratio(
                scored.Count(s => s.Couple.HumanVerdict == JudgeSession.VerdictTie), scored.Count),
            DecoderTieRate: Ratio(
                scored.Count(s => s.Decoder == JudgeSession.VerdictTie), scored.Count),
            Contingency: contingency,
            HumanMarginalA: Ratio(decided.Count(p => p.Human == JudgeSession.VerdictA), decided.Count),
            HumanMarginalB: Ratio(decided.Count(p => p.Human == JudgeSession.VerdictB), decided.Count),
            DecoderMarginalA: Ratio(decided.Count(p => p.Decoder == JudgeSession.VerdictA), decided.Count),
            DecoderMarginalB: Ratio(decided.Count(p => p.Decoder == JudgeSession.VerdictB), decided.Count),
            ByFamily: byFamily,
            AnchorCount: lot.Anchors.Count,
            AnchorCorrect: anchorCorrect,
            ThinDenominator: decided.Count < ThinDenominatorThreshold);
    }

    internal static double AgreementOf(IReadOnlyList<VerdictPair> pairs) =>
        pairs.Count == 0 ? 0.0 : pairs.Count(p => p.Human == p.Decoder) / (double)pairs.Count;

    /// <summary>
    /// κ à <b>deux catégories</b> <c>A</c>/<c>B</c> :
    /// <c>p_e = Σ_c P_humain(c) · P_décodeur(c)</c>, <c>κ = (p_o − p_e) / (1 − p_e)</c>.
    /// <para>
    /// Quand les marginales sont déséquilibrées — et elles le seront : sur <c>humanVsModel</c>,
    /// l'humain gagnera probablement la grande majorité des couples —, <c>p_e</c> s'approche de
    /// <c>p_o</c> et <b>κ s'effondre malgré un accord élevé</b>. Ce n'est pas un défaut du
    /// décodeur, c'est une propriété connue de κ sur distribution asymétrique : d'où la table de
    /// contingence et les marginales, publiées à côté.
    /// </para>
    /// <para>
    /// Marginales parfaitement dégénérées (<c>1 − p_e = 0</c>) : κ vaut <b>0</b>, jamais 1. Rendre
    /// « accord parfait » sur une division indéfinie serait le pire des deux mondes.
    /// </para>
    /// </summary>
    internal static double KappaOf(IReadOnlyList<VerdictPair> pairs)
    {
        if (pairs.Count == 0) return 0.0;

        var n = (double)pairs.Count;
        var po = pairs.Count(p => p.Human == p.Decoder) / n;

        var pe = 0.0;
        foreach (var category in new[] { JudgeSession.VerdictA, JudgeSession.VerdictB })
            pe += pairs.Count(p => p.Human == category) / n * (pairs.Count(p => p.Decoder == category) / n);

        return Math.Abs(1 - pe) < 1e-12 ? 0.0 : (po - pe) / (1 - pe);
    }

    private static double Ratio(int numerator, int denominator) =>
        denominator == 0 ? 0.0 : numerator / (double)denominator;
}
```

- [ ] **Step 4 : Exécuter, vérifier le passage**

Run : `dotnet test --filter "FullyQualifiedName~AgreementMetricsTests"`
Attendu : **21 tests PASS** (3 cas du `[Theory]` compris).

Si `Kappa_is_near_zero_when_the_two_judges_are_independent` échoue de peu, **ne pas relâcher la précision** : recompter la table de contingence attendue à la main, l'écart signale une erreur de `p_e`.

- [ ] **Step 5 : Suite complète et build**

Run : `dotnet build` puis `dotnet test`

- [ ] **Step 6 : Commit**

```bash
git add SoClover.Eval/Calibration/AgreementMetrics.cs SoClover.Tests/Eval/AgreementMetricsTests.cs
git commit -m "feat(eval): accord decodeur/humain, kappa, contingence et IC bootstrap"
```

---

## Task 6 : `CalibrationGates` + verbe `calibrate`

Les quatre portes réunies en **un verdict unique**. Sans cette agrégation, l'opérateur recolle quatre chiffres produits par trois commandes différentes, et franchit une porte sur trois portes sur quatre sans que rien ne le signale.

**Files:**
- Create: `SoClover.Eval/Calibration/CalibrationGates.cs`
- Create: `SoClover.Eval/Calibration/CalibrateCommand.cs`
- Modify: `SoClover.Eval/Program.cs` (verbe `calibrate` + usage)
- Test: `SoClover.Tests/Eval/CalibrationGatesTests.cs`

**Interfaces:**
- Consumes:
  - `AgreementReport` (T5), `CalibrationLot`, `CalibrationSet.Build/RBarByClue` (T4)
  - `DecoderFingerprint.FromManifest` (T3), `CalibrationFile.*` (T3)
  - `SoClover.Eval.Scoring.MetricsReport` (champ `Recovery`)
  - `SoClover.Eval.Io.DecodeFile.Read`, `SoClover.Eval.Io.BenchFile.Read`, `HumanFile.ReadComparisons`
  - `SoClover.Eval.Decoder.ClueDecoder.DecodeAsync(BenchBoard, Direction, string clue, int decodeIndex, string benchHash, CancellationToken)`
  - `SoClover.Eval.Config.EvalLlmConfig.{BuildConfiguration, Bind, CreateChatClient, FetchProviderModelListHashAsync}`
  - `SoClover.Eval.Human.HumanReport.ForComparisons` (cohérence intra-juge, lot suspect)
- Produces:
  - `CalibrationGates.{MinAgreement, MinKappa, MaxSaturationRecovery, MaxFloorRecovery}` → `const double`
  - `CalibrationGates.{ValidatedLabel, RejectedLabel}` → `const string`
  - `CalibrationGates.{AgreementGate, KappaGate, SaturationGate, FloorGate}` → `const string` (noms de portes)
  - `Gate(string Name, double Value, double Threshold, string Comparison, bool Passed)`
  - `CalibrationVerdict(IReadOnlyList<Gate> Gates, bool AllPassed, string Label, IReadOnlyList<string> Reasons)`
  - `CalibrationGates.Evaluate(AgreementReport agreement, double saturationRecovery, double floorRecovery)` → `CalibrationVerdict`
  - `CalibrationGates.DecodedPathForMetrics(string metricsPath)` → `string`
  - `CalibrationGates.FingerprintOfMetrics(string metricsPath)` → `string`
  - `CalibrationGates.RequireSameDecoder(string fingerprint, params string[] metricsPaths)` → `void`
  - `CalibrationReport` (record du rapport `.json`)
  - `CalibrateCommand.DefaultDecodesPerClue` → `const int = 5`
  - `CalibrateCommand.ExecuteAsync(Args, CancellationToken)` → `Task<int>`

- [ ] **Step 1 : Écrire le test des portes qui échoue**

Créer `SoClover.Tests/Eval/CalibrationGatesTests.cs` :

```csharp
using SoClover.Eval.Calibration;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using Xunit;

namespace SoClover.Tests.Eval;

public class CalibrationGatesTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"gates-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static AgreementReport Agreement(
        double agreement = 0.82, double kappa = 0.55, int decided = 60) => new(
        CoupleCount: 100, DecidedCount: decided, UnscorableCoupleCount: 0,
        Agreement: agreement, AgreementCiLow: agreement - 0.08, AgreementCiHigh: agreement + 0.08,
        Kappa: kappa, KappaCiLow: kappa - 0.10, KappaCiHigh: kappa + 0.10,
        Pabak: 2 * agreement - 1,
        HumanTieRate: 0.10, DecoderTieRate: 0.30,
        Contingency: [], HumanMarginalA: 0.6, HumanMarginalB: 0.4,
        DecoderMarginalA: 0.58, DecoderMarginalB: 0.42,
        ByFamily: [], AnchorCount: 5, AnchorCorrect: 5,
        ThinDenominator: decided < 40);

    // Les quatre seuils sont REPRIS VERBATIM du tableau « Portes d'acceptation » du PRD.
    // Un seuil qui dérive d'une spec à l'autre est exactement ce que le registre est censé
    // rendre impossible — ce test est le verrou.
    [Fact]
    public void The_four_thresholds_match_the_PRD_verbatim()
    {
        Assert.Equal(0.75, CalibrationGates.MinAgreement);
        Assert.Equal(0.40, CalibrationGates.MinKappa);
        Assert.Equal(0.95, CalibrationGates.MaxSaturationRecovery);
        Assert.Equal(0.15, CalibrationGates.MaxFloorRecovery);
    }

    [Fact]
    public void All_four_gates_passed_yields_a_single_validated_verdict()
    {
        var verdict = CalibrationGates.Evaluate(Agreement(), saturationRecovery: 0.88, floorRecovery: 0.11);

        Assert.True(verdict.AllPassed);
        Assert.Equal(CalibrationGates.ValidatedLabel, verdict.Label);
        Assert.Equal(4, verdict.Gates.Count);
        Assert.All(verdict.Gates, g => Assert.True(g.Passed));
    }

    [Theory]
    [InlineData(0.74, 0.55, 0.88, 0.11, CalibrationGates.AgreementGate)]
    [InlineData(0.82, 0.31, 0.88, 0.11, CalibrationGates.KappaGate)]
    [InlineData(0.82, 0.55, 0.97, 0.11, CalibrationGates.SaturationGate)]
    [InlineData(0.82, 0.55, 0.88, 0.19, CalibrationGates.FloorGate)]
    public void Any_single_failing_gate_sends_the_decoder_back_to_P3(
        double agreement, double kappa, double saturation, double floor, string failing)
    {
        var verdict = CalibrationGates.Evaluate(Agreement(agreement, kappa), saturation, floor);

        Assert.False(verdict.AllPassed);
        Assert.Equal(CalibrationGates.RejectedLabel, verdict.Label);
        Assert.False(verdict.Gates.Single(g => g.Name == failing).Passed);
        Assert.Contains(verdict.Reasons, r => r.Contains(failing, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_failing_gate_is_named_when_several_fall_at_once()
    {
        var verdict = CalibrationGates.Evaluate(
            Agreement(agreement: 0.60, kappa: 0.20), saturationRecovery: 0.99, floorRecovery: 0.40);

        Assert.Equal(4, verdict.Reasons.Count(r => r.StartsWith("porte non franchie", StringComparison.Ordinal)));
        Assert.All(verdict.Gates, g => Assert.False(g.Passed));
    }

    [Fact]
    public void A_threshold_met_exactly_passes()
    {
        var verdict = CalibrationGates.Evaluate(
            Agreement(agreement: CalibrationGates.MinAgreement, kappa: CalibrationGates.MinKappa),
            saturationRecovery: CalibrationGates.MaxSaturationRecovery,
            floorRecovery: CalibrationGates.MaxFloorRecovery);

        Assert.True(verdict.AllPassed);
    }

    // Le dénominateur fragile n'est PAS une cinquième porte : le chiffre est publié, marqué.
    [Fact]
    public void A_thin_denominator_is_reported_without_becoming_a_fifth_gate()
    {
        var verdict = CalibrationGates.Evaluate(
            Agreement(decided: 28), saturationRecovery: 0.88, floorRecovery: 0.11);

        Assert.True(verdict.AllPassed);
        Assert.Contains(verdict.Reasons, r => r.Contains("28"));
    }

    // ---- Empreintes ------------------------------------------------------------

    [Fact]
    public void DecodedPathForMetrics_finds_the_sibling_decoded_file()
    {
        Assert.Equal(
            Path.Combine("eval", "runs", "run-x.decoded.jsonl"),
            CalibrationGates.DecodedPathForMetrics(Path.Combine("eval", "runs", "run-x.metrics.json")));
    }

    // Une porte franchie sur un instrument et des chiffres publiés par un autre : c'est
    // exactement l'accident que le cycle vient réparer. Il doit être impossible, pas déconseillé.
    [Fact]
    public void Refuses_metrics_produced_by_another_decoder()
    {
        var metrics = WriteMetricsWithDecoder("plancher", cluePromptVersion: 1);
        var fingerprint = DecoderFingerprint.FromManifest(
            DecoderFingerprintTests.Manifest(cluePromptVersion: 2));

        var ex = Assert.Throws<InvalidOperationException>(
            () => CalibrationGates.RequireSameDecoder(fingerprint, metrics));

        Assert.Contains("empreinte", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Accepts_metrics_produced_by_the_same_decoder()
    {
        var metrics = WriteMetricsWithDecoder("plancher", cluePromptVersion: 2);
        var fingerprint = DecoderFingerprint.FromManifest(
            DecoderFingerprintTests.Manifest(cluePromptVersion: 2));

        CalibrationGates.RequireSameDecoder(fingerprint, metrics);
    }

    // decodesPerClue à 5 en calibration et 3 dans les runs : les empreintes ne divergent pas.
    [Fact]
    public void A_different_decodes_per_clue_does_not_break_the_fingerprint_check()
    {
        var metrics = WriteMetricsWithDecoder("plancher", cluePromptVersion: 2, decodesPerClue: 3);
        var fingerprint = DecoderFingerprint.FromManifest(
            DecoderFingerprintTests.Manifest(cluePromptVersion: 2, decodesPerClue: 5));

        CalibrationGates.RequireSameDecoder(fingerprint, metrics);
    }

    [Fact]
    public void Refuses_metrics_whose_decoded_sibling_is_missing()
    {
        var metrics = Path.Combine(_directory, "orphelin.metrics.json");
        File.WriteAllText(metrics, "{}");

        Assert.Throws<FileNotFoundException>(
            () => CalibrationGates.RequireSameDecoder("3f2a91c4e0d1", metrics));
    }

    private string WriteMetricsWithDecoder(
        string name, int? cluePromptVersion, int decodesPerClue = 3)
    {
        var metrics = Path.Combine(_directory, $"{name}.metrics.json");
        File.WriteAllText(metrics, "{}");

        DecodeFile.WriteManifest(
            Path.Combine(_directory, $"{name}.decoded.jsonl"),
            DecoderFingerprintTests.Manifest(
                cluePromptVersion: cluePromptVersion, decodesPerClue: decodesPerClue));

        return metrics;
    }
}
```

- [ ] **Step 2 : Exécuter, vérifier l'échec**

Run : `dotnet test --filter "FullyQualifiedName~CalibrationGatesTests"`
Attendu : **échec de compilation** — `CalibrationGates` n'existe pas. (`DecoderFingerprintTests.Manifest` est déjà `internal static` depuis T3.)

- [ ] **Step 3 : Écrire `Calibration/CalibrationGates.cs`**

```csharp
using System.Globalization;
using SoClover.Eval.Io;

namespace SoClover.Eval.Calibration;

public sealed record Gate(string Name, double Value, double Threshold, string Comparison, bool Passed);

public sealed record CalibrationVerdict(
    IReadOnlyList<Gate> Gates, bool AllPassed, string Label, IReadOnlyList<string> Reasons);

/// <summary>
/// Les <b>quatre</b> portes d'acceptation du PRD, réunies en un verdict unique.
/// <para>
/// Sans cette agrégation, l'opérateur recolle quatre chiffres produits par trois commandes
/// différentes, et franchit « une porte sur trois » portes sur quatre sans que rien ne le
/// signale. Les seuils sont <b>repris verbatim</b> du tableau « Portes d'acceptation » : un seuil
/// qui dérive d'une spec à l'autre est exactement ce que le registre est censé rendre impossible.
/// </para>
/// </summary>
public static class CalibrationGates
{
    public const double MinAgreement = 0.75;
    public const double MinKappa = 0.40;
    public const double MaxSaturationRecovery = 0.95;
    public const double MaxFloorRecovery = 0.15;

    public const string AgreementGate = "accord";
    public const string KappaGate = "kappa";
    public const string SaturationGate = "non-saturation";
    public const string FloorGate = "plancher";

    public const string ValidatedLabel = "DÉCODEUR VALIDÉ";
    public const string RejectedLabel = "DÉCODEUR RENVOYÉ EN P3";

    /// <summary>Marge d'arrondi : un seuil est une règle éditoriale, l'égalité doit passer.</summary>
    private const double ThresholdTolerance = 1e-9;

    public static CalibrationVerdict Evaluate(
        AgreementReport agreement, double saturationRecovery, double floorRecovery)
    {
        var gates = new[]
        {
            AtLeast(AgreementGate, agreement.Agreement, MinAgreement),
            AtLeast(KappaGate, agreement.Kappa, MinKappa),
            AtMost(SaturationGate, saturationRecovery, MaxSaturationRecovery),
            AtMost(FloorGate, floorRecovery, MaxFloorRecovery),
        };

        var reasons = gates
            .Where(g => !g.Passed)
            .Select(g => string.Create(CultureInfo.GetCultureInfo("fr-FR"),
                $"porte non franchie : {g.Name} = {g.Value:0.000} ({g.Comparison} {g.Threshold:0.00} requis)"))
            .ToList();

        // Le dénominateur fragile n'est PAS une cinquième porte : le chiffre est publié, MARQUÉ.
        // Un accord de 0,80 sur 28 couples a un IC qui traverse la porte, et le rapport doit le dire.
        if (agreement.ThinDenominator)
            reasons.Add(
                $"⚠ dénominateur de l'accord : {agreement.DecidedCount} couple(s), " +
                $"sous les {AgreementMetrics.ThinDenominatorThreshold} attendus — chiffre FRAGILE, " +
                "lire l'IC avant de conclure");

        if (agreement.UnscorableCoupleCount > 0)
            reasons.Add(
                $"{agreement.UnscorableCoupleCount} couple(s) exclus : un indice sans aucun décodage exploitable");

        var allPassed = gates.All(g => g.Passed);
        return new CalibrationVerdict(
            gates.ToList().AsReadOnly(),
            allPassed,
            allPassed ? ValidatedLabel : RejectedLabel,
            reasons.AsReadOnly());
    }

    private static Gate AtLeast(string name, double value, double threshold) =>
        new(name, value, threshold, "≥", value >= threshold - ThresholdTolerance);

    private static Gate AtMost(string name, double value, double threshold) =>
        new(name, value, threshold, "≤", value <= threshold + ThresholdTolerance);

    // ── Empreintes ──────────────────────────────────────────────────────────

    private const string MetricsSuffix = ".metrics.json";
    private const string DecodedSuffix = ".decoded.jsonl";

    /// <summary>
    /// <c>&lt;run&gt;.metrics.json</c> → <c>&lt;run&gt;.decoded.jsonl</c>.
    /// <para>
    /// <c>MetricsReport</c> ne porte aucune empreinte de décodeur, et lui en ajouter une changerait
    /// le schéma des <c>.metrics.json</c> déjà produits. Le fichier frère porte déjà toute
    /// l'information — c'est lui qu'on interroge.
    /// </para>
    /// </summary>
    public static string DecodedPathForMetrics(string metricsPath)
    {
        if (!metricsPath.EndsWith(MetricsSuffix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Chemin de métriques attendu en « {MetricsSuffix} », reçu : {metricsPath}", nameof(metricsPath));

        return metricsPath[..^MetricsSuffix.Length] + DecodedSuffix;
    }

    public static string FingerprintOfMetrics(string metricsPath) =>
        DecoderFingerprint.FromManifest(DecodeFile.Read(DecodedPathForMetrics(metricsPath)).Manifest);

    /// <summary>
    /// Refuse si l'une des métriques citées n'a pas été produite par le décodeur calibré. Les
    /// quatre portes doivent porter sur <b>le même</b> décodeur : deux <c>recovery</c> d'empreintes
    /// différentes ne se comparent pas, au même titre que deux runs de <c>benchHash</c> différents,
    /// que <c>compare</c> refuse déjà.
    /// </summary>
    public static void RequireSameDecoder(string fingerprint, params string[] metricsPaths)
    {
        foreach (var path in metricsPaths)
        {
            var other = FingerprintOfMetrics(path);
            if (!string.Equals(other, fingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"{path} a été produit par le décodeur d'empreinte {other}, la calibration porte " +
                    $"sur {fingerprint}. Les quatre portes doivent porter sur le MÊME décodeur — " +
                    "re-décoder ce run avec le décodeur courant (decode --force) puis le re-scorer.");
        }
    }
}
```

- [ ] **Step 4 : Exécuter, vérifier le passage**

Run : `dotnet test --filter "FullyQualifiedName~CalibrationGatesTests"`
Attendu : **14 tests PASS** (4 cas du `[Theory]` compris).

- [ ] **Step 5 : Écrire le rapport et le verbe — `Calibration/CalibrateCommand.cs`**

```csharp
using System.Diagnostics;
using System.Globalization;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Cli;
using SoClover.Eval.Config;
using SoClover.Eval.Decoder;
using SoClover.Eval.Human;
using SoClover.Eval.Io;
using SoClover.Eval.Scoring;
using SoClover.Infrastructure.AI.Prompts;

namespace SoClover.Eval.Calibration;

/// <summary>
/// Le rapport <c>calibration.&lt;date&gt;-&lt;empreinte&gt;.json</c>. <b>Committé</b> : avec les
/// deux corpus humains, il est l'investissement irremplaçable du chantier.
/// </summary>
public sealed record CalibrationReport(
    string CalibrationId,
    DateTime CreatedAtUtc,
    string DecoderFingerprint,
    string ModelId,
    int? CluePromptVersion,
    int DecodesPerClue,
    double Epsilon,
    AgreementReport Agreement,
    double SaturationRecovery,
    double FloorRecovery,
    IReadOnlyList<Gate> Gates,
    bool AllGatesPassed,
    string Verdict,
    IReadOnlyList<string> Reasons,
    double IntraJudgeAgreement,
    int DuplicatePairCount,
    bool IntraJudgeBelowGate,
    bool Position1Suspect,
    bool AnchorSuspect,
    string? OperatorNotes);

/// <summary>
/// Verbe <c>calibrate</c> : re-décode les indices de <c>comparisons.dev.jsonl</c> avec le décodeur
/// <b>courant</b>, calcule accord et κ, lit les deux portes externes, rend un verdict unique.
/// <para>
/// Verbe <b>autonome</b> et non une jointure sur les <c>.decoded.jsonl</c> : les indices
/// <c>assisted</c> n'appartiennent à aucun run (<c>HumanRunExport.Build</c> ne projette que les
/// lignes <c>elicitation</c>), et joindre exigerait de vérifier que quatre runs ont été décodés
/// par le même décodeur. Le verbe autonome garantit en plus que les <b>deux options d'un couple
/// sont décodées à l'identique</b>.
/// </para>
/// </summary>
public static class CalibrateCommand
{
    /// <summary>
    /// Avec <c>half_rate = 0,656</c>, une masse de couples serait ex æquo à R̄ = 0,5 des deux côtés.
    /// Passer de 3 à 5 décodages fait passer la granularité de 1/6 à 1/10 et desserre le
    /// dénominateur de l'accord. Le défaut de <c>decode</c> reste 3, inchangé.
    /// </summary>
    public const int DefaultDecodesPerClue = 5;

    public static async Task<int> ExecuteAsync(Args args, CancellationToken ct)
    {
        var comparisonsPath = args.Get("comparisons")
                              ?? Path.Combine("eval", "human", "comparisons.dev.jsonl");
        var outDirectory = args.Get("out") ?? Path.Combine("eval", "human");
        var decodesPerClue = args.GetInt("decodes", DefaultDecodesPerClue);
        var epsilon = ParseEpsilon(args.Get("epsilon"));
        var force = args.Has("force");
        var notes = args.Get("notes");
        var modelSnapshotDate = args.Get("model-snapshot") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");

        var comparisons = HumanFile.ReadComparisons(comparisonsPath);
        var benchPath = args.Get("bench") ?? comparisons.Manifest.BenchFile;
        var bench = BenchFile.Read(benchPath);
        HumanFile.RequireBench(comparisonsPath, comparisons.Manifest.BenchHash, bench);

        var lot = CalibrationSet.Build(comparisons);
        if (lot.Couples.Count == 0)
            throw new InvalidOperationException(
                $"{comparisonsPath} ne porte aucun couple exploitable. Une porte franchie sur un " +
                "lot partiel n'est pas une porte.");

        // ---- Décodeur courant, et son empreinte ----------------------------
        var config = EvalLlmConfig.BuildConfiguration();
        var llmOptions = EvalLlmConfig.Bind(config, "Decoder");
        var opts = llmOptions.Value;

        using var chatClient = EvalLlmConfig.CreateChatClient(llmOptions);
        var loader = new FilePromptLoader();
        var cluePromptPath = Path.Combine(
            AppContext.BaseDirectory, "Decoder", "Prompts", "fr", "decode-clue.md");
        var decoder = new ClueDecoder(
            chatClient, loader, cluePromptPath, opts.DefaultModel,
            (float)opts.DefaultTemperature, (float?)opts.TopP, opts.MaxOutputTokens);

        var fingerprint = DecoderFingerprint.Compute(
            opts.DefaultModel, cluePromptPath, decoder.PromptVersion,
            opts.DefaultTemperature, opts.TopP, opts.MaxOutputTokens);

        // Les quatre portes doivent porter sur le MÊME décodeur — vérifié AVANT de dépenser
        // ~900 appels au LLM.
        var saturationMetrics = args.Require("saturation-metrics");
        var floorMetrics = args.Require("floor-metrics");
        CalibrationGates.RequireSameDecoder(fingerprint, saturationMetrics, floorMetrics);

        var calibrationId = $"{DateTime.UtcNow:yyyyMMdd}-{fingerprint}";
        var path = CalibrationFile.PathFor(outDirectory, calibrationId);
        var existing = force ? null : CalibrationFile.ReadOrNull(path);

        if (existing is null)
        {
            var providerModelListHash = await EvalLlmConfig
                .FetchProviderModelListHashAsync(opts, ct).ConfigureAwait(false);

            CalibrationFile.WriteManifest(path, new CalibrationManifest(
                Kind: "manifest",
                CalibrationId: calibrationId,
                CreatedAtUtc: DateTime.UtcNow,
                ComparisonsFile: comparisonsPath.Replace('\\', '/'),
                BenchFile: benchPath.Replace('\\', '/'),
                BenchHash: bench.Manifest.BenchHash,
                CoupleCount: lot.Couples.Count + lot.Anchors.Count,
                ClueCount: lot.Clues.Count,
                DecoderFingerprint: fingerprint,
                Provider: opts.Provider.ToString(),
                BaseUrl: opts.BaseUrl,
                ModelId: opts.DefaultModel,
                ModelSnapshotDate: modelSnapshotDate,
                ProviderModelListHash: providerModelListHash,
                Temperature: opts.DefaultTemperature,
                TopP: opts.TopP,
                MaxOutputTokens: opts.MaxOutputTokens,
                CluePromptFile: cluePromptPath.Replace('\\', '/'),
                CluePromptVersion: decoder.PromptVersion,
                DecodesPerClue: decodesPerClue,
                Epsilon: epsilon,
                HarnessVersion: CalibrationFile.HarnessVersion,
                OperatorNotes: notes));
        }
        else if (existing.Manifest.DecodesPerClue != decodesPerClue)
        {
            // Mélanger 3 et 5 décodages dans un même fichier produirait des R̄ de granularités
            // différentes selon l'indice. Même règle que decode, pour n'en avoir qu'une à retenir.
            throw new InvalidOperationException(
                $"{path} porte decodesPerClue={existing.Manifest.DecodesPerClue}, incompatible avec " +
                $"--decodes {decodesPerClue}. Utiliser --force pour repartir de zéro.");
        }
        else if (Math.Abs(existing.Manifest.Epsilon - epsilon) > 1e-12)
        {
            throw new InvalidOperationException(
                $"{path} porte epsilon={existing.Manifest.Epsilon}, incompatible avec --epsilon {epsilon}. " +
                "ε se décide AVANT de lire l'accord : le changer en cours de calibration est une " +
                "faute de protocole. Utiliser --force en connaissance de cause.");
        }

        var alreadyDecoded = existing is null
            ? []
            : existing.Decodes.Select(d => (d.BoardId, d.Direction, d.Clue, d.DecodeIndex)).ToHashSet();

        var boards = bench.Boards.ToDictionary(b => b.BoardId, StringComparer.Ordinal);

        Console.WriteLine($"calibration {calibrationId}");
        Console.WriteLine($"  fichier        : {path}");
        Console.WriteLine($"  comparaisons   : {comparisonsPath}");
        Console.WriteLine($"  couples        : {lot.Couples.Count} principaux + {lot.Anchors.Count} ancre(s)");
        Console.WriteLine($"  indices        : {lot.Clues.Count} distinct(s) × {decodesPerClue} décodages");
        Console.WriteLine($"  modèle         : {opts.DefaultModel} (snapshot {modelSnapshotDate})");
        Console.WriteLine($"  prompt         : clue v{decoder.PromptVersion}");
        Console.WriteLine($"  empreinte      : {fingerprint}");
        Console.WriteLine($"  ε              : {epsilon.ToString("0.###", CultureInfo.InvariantCulture)}");

        var stopwatch = Stopwatch.StartNew();
        var done = 0;

        foreach (var clue in lot.Clues)
        {
            ct.ThrowIfCancellationRequested();

            var board = boards[clue.BoardId];
            var direction = Enum.Parse<Direction>(clue.Direction);

            for (var index = 0; index < decodesPerClue; index++)
            {
                if (alreadyDecoded.Contains((clue.BoardId, clue.Direction, clue.Clue, index)))
                    continue;

                // ShuffleSeed.ForClue ne dépend NI de la direction NI de l'indice : les deux
                // options d'un couple voient le même ordre à decodeIndex égal. L'ordre de
                // présentation ne peut donc structurellement pas expliquer une préférence du
                // décodeur — le contrôle est apparié, gratuitement.
                var line = await decoder
                    .DecodeAsync(board, direction, clue.Clue, index, bench.Manifest.BenchHash, ct)
                    .ConfigureAwait(false);

                CalibrationFile.AppendDecode(path, CalibrationDecode.From(line, clue.Clue));
            }

            done++;
            if (done % 20 == 0)
                Console.WriteLine($"  {done}/{lot.Clues.Count} indices — écoulé {stopwatch.Elapsed:hh\\:mm\\:ss}");
        }

        // ---- Agrégation ----------------------------------------------------
        var decodes = CalibrationFile.Read(path);
        var rBar = CalibrationSet.RBarByClue(decodes);
        var agreement = AgreementMetrics.Compute(
            lot, rBar, epsilon,
            args.GetInt("bootstrap", Bootstrap.DefaultIterations),
            args.GetLong("seed", AgreementMetrics.DefaultBootstrapSeed));

        var saturationRecovery = ReadRecovery(saturationMetrics);
        var floorRecovery = ReadRecovery(floorMetrics);
        var verdict = CalibrationGates.Evaluate(agreement, saturationRecovery, floorRecovery);

        var humanReport = HumanReport.ForComparisons(comparisons);

        var report = new CalibrationReport(
            CalibrationId: calibrationId,
            CreatedAtUtc: DateTime.UtcNow,
            DecoderFingerprint: fingerprint,
            ModelId: opts.DefaultModel,
            CluePromptVersion: decoder.PromptVersion,
            DecodesPerClue: decodesPerClue,
            Epsilon: epsilon,
            Agreement: agreement,
            SaturationRecovery: saturationRecovery,
            FloorRecovery: floorRecovery,
            Gates: verdict.Gates,
            AllGatesPassed: verdict.AllPassed,
            Verdict: verdict.Label,
            Reasons: verdict.Reasons,
            IntraJudgeAgreement: humanReport.IntraJudgeAgreement,
            DuplicatePairCount: humanReport.DuplicatePairCount,
            IntraJudgeBelowGate: humanReport.DuplicatePairCount > 0
                                 && humanReport.IntraJudgeAgreement < CalibrationGates.MinAgreement,
            Position1Suspect: humanReport.Position1Suspect,
            AnchorSuspect: humanReport.AnchorSuspect,
            OperatorNotes: notes);

        var reportPath = CalibrationFile.ReportPathFor(path);
        File.WriteAllText(reportPath, EvalJson.Serialize(report));

        Print(report, stopwatch.Elapsed);
        Console.WriteLine($"rapport écrit : {reportPath}");
        return 0;
    }

    private static double ParseEpsilon(string? raw) =>
        raw is null
            ? AgreementMetrics.DefaultEpsilon
            : double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v
                : throw new ArgumentException($"--epsilon attend un décimal, valeur reçue : \"{raw}\"");

    private static double ReadRecovery(string metricsPath)
    {
        var metrics = EvalJson.Deserialize<MetricsReport>(File.ReadAllText(metricsPath));
        return metrics.Recovery;
    }

    private static void Print(CalibrationReport r, TimeSpan elapsed)
    {
        static string N(double v) => v.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"));

        var a = r.Agreement;

        Console.WriteLine();
        Console.WriteLine($"terminé en {elapsed:hh\\:mm\\:ss}");
        Console.WriteLine();
        Console.WriteLine("accord décodeur / humain");
        Console.WriteLine($"  accord (hors égalités) {N(a.Agreement)}   IC 95 % [{N(a.AgreementCiLow)} ; {N(a.AgreementCiHigh)}]");
        Console.WriteLine($"  dénominateur           {a.DecidedCount} / {a.CoupleCount} couples");
        Console.WriteLine($"  égalités humain        {N(a.HumanTieRate)}");
        Console.WriteLine($"  égalités décodeur      {N(a.DecoderTieRate)}");
        Console.WriteLine();
        Console.WriteLine("Cohen's κ");
        Console.WriteLine($"  κ global               {N(a.Kappa)}   IC 95 % [{N(a.KappaCiLow)} ; {N(a.KappaCiHigh)}]");
        Console.WriteLine($"  PABAK (diagnostic)     {N(a.Pabak)}   ← jamais une porte");
        foreach (var f in a.ByFamily)
            Console.WriteLine($"  κ {f.Family,-18} {N(f.Kappa)}   accord {N(f.Agreement)}   (n={f.DecidedCount})");
        Console.WriteLine();
        Console.WriteLine("table de contingence (humain × décodeur, sur les couples doublement tranchés)");
        foreach (var cell in a.Contingency)
            Console.WriteLine($"  humain {cell.HumanVerdict} × décodeur {cell.DecoderVerdict}   {cell.Count,4}");
        Console.WriteLine($"  marginales humain      A {N(a.HumanMarginalA)} / B {N(a.HumanMarginalB)}");
        Console.WriteLine($"  marginales décodeur    A {N(a.DecoderMarginalA)} / B {N(a.DecoderMarginalB)}");
        Console.WriteLine();
        Console.WriteLine($"ancres                   {a.AnchorCorrect} / {a.AnchorCount}   (hors calcul principal)");
        Console.WriteLine($"cohérence intra-juge     {N(r.IntraJudgeAgreement)}   (sur {r.DuplicatePairCount} doublon(s))");

        // Exiger du décodeur un accord supérieur à celui du juge avec lui-même n'a aucun sens.
        if (r.IntraJudgeBelowGate)
            Console.WriteLine(
                $"  ⚠ la cohérence intra-juge est sous {N(CalibrationGates.MinAgreement)} : la porte d'accord " +
                "est INATTEIGNABLE PAR CONSTRUCTION. C'est une information sur le corpus, pas sur le décodeur.");

        // Le PRD demande que le lot suspect soit consigné DANS LE REGISTRE.
        if (r.Position1Suspect)
            Console.WriteLine("  ⚠ LOT SUSPECT (position 1) — à consigner au registre avant d'en tirer une porte.");
        if (r.AnchorSuspect)
            Console.WriteLine("  ⚠ LOT SUSPECT (ancres ratées) — à consigner au registre avant d'en tirer une porte.");

        Console.WriteLine();
        Console.WriteLine("portes");
        foreach (var g in r.Gates)
            Console.WriteLine($"  {(g.Passed ? "✓" : "✗")} {g.Name,-16} {N(g.Value)}   ({g.Comparison} {g.Threshold:0.00})");
        Console.WriteLine();
        Console.WriteLine($"{r.Verdict}   empreinte {r.DecoderFingerprint}");
        foreach (var reason in r.Reasons)
            Console.WriteLine($"    — {reason}");

        if (!r.AllGatesPassed)
        {
            Console.WriteLine();
            Console.WriteLine(
                "  Retour en P3 : une variable à la fois — prompt decode-clue (version incrémentée), " +
                "puis modèle, puis température / maxOutputTokens, puis decodesPerClue. " +
                "INTERDIT : ajuster le décodeur en regardant les désaccords couple par couple — " +
                "on lit au plus une dizaine de désaccords pour DIAGNOSTIQUER, jamais pour ajuster.");
        }
        Console.WriteLine();
    }
}
```

- [ ] **Step 6 : Câbler le verbe dans `Program.cs`**

Dans le `switch` de `EvalProgram.Main`, après la ligne `"compare" => …` :

```csharp
                "calibrate" => CalibrateCommand.ExecuteAsync(cliArgs, CancellationToken.None),
```

Ajouter `using SoClover.Eval.Calibration;` en tête du fichier, et compléter le bloc `Usage()` — après la ligne `human-report` :

```
              calibrate     P6 : accord decodeur/humain, kappa, quatre portes, verdict unique
```

et, après le bloc « Sous-ensemble », ajouter :

```
            Calibration (calibrate) :
              --comparisons <comparisons.jsonl>  corpus de la séance B (défaut eval/human/)
              --decodes 5                        granularité de R̄ ; le défaut de `decode` reste 3
              --saturation-metrics <x.metrics.json>  porte de non-saturation (recovery ≤ 0,95)
              --floor-metrics      <x.metrics.json>  porte du plancher       (recovery ≤ 0,15)
              --epsilon 0                        marge du verdict décodeur ; se décide AVANT de
                                                 lire l'accord, et part au manifeste
```

- [ ] **Step 7 : Vérifier le câblage**

Run : `dotnet run --project SoClover.Eval -- calibrate`
Attendu : erreur explicite `ERREUR : Argument requis manquant : --saturation-metrics` **ou** l'absence de `eval/human/comparisons.dev.jsonl` — dans les deux cas un message français, jamais une trace de pile.

Run : `dotnet run --project SoClover.Eval` (sans verbe)
Attendu : l'usage liste `calibrate`.

- [ ] **Step 8 : Suite complète et build**

Run : `dotnet build` puis `dotnet test`

- [ ] **Step 9 : Commit**

```bash
git add SoClover.Eval/Calibration/CalibrationGates.cs SoClover.Eval/Calibration/CalibrateCommand.cs SoClover.Eval/Program.cs SoClover.Tests/Eval/CalibrationGatesTests.cs
git commit -m "feat(eval): verbe calibrate — quatre portes reunies en un verdict unique"
```

---

## Task 7 : le statut `calibré` au registre

Le drapeau `--calibration` de `score`. **Refus bruyant, jamais de dégradation silencieuse** : qui passe le drapeau veut publier une ligne défendable ; produire à la place une ligne mal étiquetée serait le pire des deux mondes.

**Files:**
- Modify: `SoClover.Eval/Io/LedgerWriter.cs` (statut, bandeau)
- Modify: `SoClover.Eval/Scoring/ScoreCommand.cs:41-62` (drapeau `--calibration`)
- Modify: `SoClover.Eval/Program.cs` (usage)
- Test: `SoClover.Tests/Eval/ScoreCalibrationStatusTests.cs`
- Filet (non modifiés) : `SoClover.Tests/Eval/LedgerWriterTests.cs`, `ScoreCommandNotesTests.cs`

**Interfaces:**
- Consumes: `CalibrationReport` (T6), `DecoderFingerprint.FromManifest` (T3), `LedgerEntry`, `DecodeContents`
- Produces:
  - `LedgerWriter.CalibratedStatus` → `const string = "calibré"`
  - `LedgerWriter.CalibratedStatusFor(string fingerprint)` → `string` (`"calibré (3f2a91c4e0d1)"`)
  - `ScoreCommand.ResolveStatus(CalibrationReport? calibration, DecodeContents? decoded, string calibrationPathForMessages)` → `string` (`internal`)

- [ ] **Step 1 : Écrire le test qui échoue**

Créer `SoClover.Tests/Eval/ScoreCalibrationStatusTests.cs` :

```csharp
using SoClover.Eval.Calibration;
using SoClover.Eval.Decoder;
using SoClover.Eval.Io;
using SoClover.Eval.Scoring;
using Xunit;

namespace SoClover.Tests.Eval;

public class ScoreCalibrationStatusTests
{
    private const string Fingerprint = "3f2a91c4e0d1";

    private static DecodeContents Decoded(int? cluePromptVersion = 2) =>
        new(DecoderFingerprintTests.Manifest(cluePromptVersion: cluePromptVersion), [], []);

    private static CalibrationReport Report(bool allPassed = true, string? fingerprint = null) => new(
        CalibrationId: "20260805-x",
        CreatedAtUtc: new DateTime(2026, 8, 5, 9, 0, 0, DateTimeKind.Utc),
        DecoderFingerprint: fingerprint ?? DecoderFingerprint.FromManifest(Decoded().Manifest),
        ModelId: "qwen/qwen3-8b",
        CluePromptVersion: 2,
        DecodesPerClue: 5,
        Epsilon: 0.0,
        Agreement: null!,
        SaturationRecovery: 0.88,
        FloorRecovery: 0.11,
        Gates: [],
        AllGatesPassed: allPassed,
        Verdict: allPassed ? CalibrationGates.ValidatedLabel : CalibrationGates.RejectedLabel,
        Reasons: [],
        IntraJudgeAgreement: 0.9,
        DuplicatePairCount: 10,
        IntraJudgeBelowGate: false,
        Position1Suspect: false,
        AnchorSuspect: false,
        OperatorNotes: null);

    // Sans le drapeau, le comportement actuel est INCHANGÉ.
    [Fact]
    public void Without_the_flag_the_status_stays_pre_calibration()
    {
        Assert.Equal(
            LedgerWriter.PreCalibrationStatus,
            ScoreCommand.ResolveStatus(calibration: null, Decoded(), "—"));
    }

    [Fact]
    public void With_four_passed_gates_and_a_matching_fingerprint_the_status_becomes_calibrated()
    {
        var status = ScoreCommand.ResolveStatus(Report(), Decoded(), "calibration.json");

        Assert.StartsWith(LedgerWriter.CalibratedStatus, status);
    }

    // La ligne doit PORTER son empreinte : c'est ce qui rendra lisible, dans six mois, l'effet
    // du passage de decode-clue v1 à v2. Et les 18 colonnes restent 18.
    [Fact]
    public void The_calibrated_status_carries_the_decoder_fingerprint()
    {
        var expected = DecoderFingerprint.FromManifest(Decoded().Manifest);

        Assert.Contains(expected, ScoreCommand.ResolveStatus(Report(), Decoded(), "calibration.json"));
    }

    // Refus BRUYANT, jamais de repli silencieux en pré-calibration.
    [Fact]
    public void Refuses_loudly_when_a_gate_has_not_been_passed()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ScoreCommand.ResolveStatus(Report(allPassed: false), Decoded(), "calibration.json"));

        Assert.Contains("portes", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(LedgerWriter.PreCalibrationStatus, ex.Message);
    }

    [Fact]
    public void Refuses_when_the_run_was_decoded_by_another_decoder()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ScoreCommand.ResolveStatus(Report(), Decoded(cluePromptVersion: 1), "calibration.json"));

        Assert.Contains("empreinte", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Refuses_a_run_that_was_never_decoded()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ScoreCommand.ResolveStatus(Report(), decoded: null, "calibration.json"));

        Assert.Contains("décod", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Registre --------------------------------------------------------------

    [Fact]
    public void A_calibrated_row_still_carries_exactly_eighteen_columns()
    {
        var path = Path.Combine(Path.GetTempPath(), $"LEDGER-{Guid.NewGuid():N}.md");
        try
        {
            LedgerWriter.Append(path, Entry(LedgerWriter.CalibratedStatusFor(Fingerprint)));

            var row = LedgerWriter.ReadRows(path).Single();

            Assert.Equal(LedgerWriter.ColumnCount, row.Split('|').Length - 2);
            Assert.Contains("calibré", row);
            Assert.Contains(Fingerprint, row);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // Les lignes existantes ne sont JAMAIS réécrites : les runs antérieurs obtiennent une ligne
    // `calibré` par re-score, et les deux lignes coexistent.
    [Fact]
    public void A_calibrated_row_is_appended_next_to_the_pre_calibration_one()
    {
        var path = Path.Combine(Path.GetTempPath(), $"LEDGER-{Guid.NewGuid():N}.md");
        try
        {
            LedgerWriter.Append(path, Entry(LedgerWriter.PreCalibrationStatus));
            LedgerWriter.Append(path, Entry(LedgerWriter.CalibratedStatusFor(Fingerprint)));

            var rows = LedgerWriter.ReadRows(path);

            Assert.Equal(2, rows.Count);
            Assert.Contains(LedgerWriter.PreCalibrationStatus, rows[0]);
            Assert.Contains(LedgerWriter.CalibratedStatus, rows[1]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void The_header_explains_the_calibrated_status_and_the_fingerprint()
    {
        var path = Path.Combine(Path.GetTempPath(), $"LEDGER-{Guid.NewGuid():N}.md");
        try
        {
            LedgerWriter.Append(path, Entry(LedgerWriter.PreCalibrationStatus));

            var text = File.ReadAllText(path);

            Assert.Contains("calibré", text);
            Assert.Contains("empreinte", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static LedgerEntry Entry(string status) => new(
        new DateOnly(2026, 8, 5), "20260728-v5-gemma-d79a63b9", "eval/boards.dev.jsonl",
        "fr/board-clues-per-direction.md", 5, "gemma-4-12b-qat", "2026-07-28",
        "temp 1.0 / topP 0.95 / maxRetries 0",
        new MetricsReport(
            "20260728-v5-gemma-d79a63b9", "eval/boards.dev.jsonl", "416b819a41a1", 40, 160,
            0.94, 0.81, 0.02, 0.363, 0.41, 0.656, 0.55, 0.12, [], 0.01, 160, 160,
            new Dictionary<(string, string), double>()),
        status, "baseline v5", "neutre", "thinking OFF, ctx 16k");
}
```

- [ ] **Step 2 : Exécuter, vérifier l'échec**

Run : `dotnet test --filter "FullyQualifiedName~ScoreCalibrationStatusTests"`
Attendu : **échec de compilation** — `LedgerWriter.CalibratedStatus` et `ScoreCommand.ResolveStatus` n'existent pas.

- [ ] **Step 3 : Étendre `Io/LedgerWriter.cs`**

Ajouter, juste après `PreCalibrationStatus` :

```csharp
    /// <summary>
    /// Statut d'une ligne défendable : le décodeur a franchi les <b>quatre</b> portes du PRD.
    /// <para>
    /// La cellule porte aussi l'<b>empreinte du décodeur</b> — c'est ce qui rendra lisible, dans
    /// six mois, l'effet du passage de <c>decode-clue</c> v1 à v2 : les deux lignes coexistent,
    /// aucune n'est réécrite, et l'empreinte dit laquelle se compare à laquelle.
    /// </para>
    /// </summary>
    public const string CalibratedStatus = "calibré";

    public static string CalibratedStatusFor(string decoderFingerprint) =>
        $"{CalibratedStatus} ({decoderFingerprint})";
```

Puis compléter le `Header` — insérer, avant la ligne vide qui précède le tableau :

```
        >
        > Statut `calibré (<empreinte>)` : le décodeur a franchi les **quatre** portes (accord
        > ≥ 0,75, κ ≥ 0,40, non-saturation ≤ 0,95, plancher ≤ 0,15), consignées dans un fichier
        > `eval/human/calibration.<date>-<empreinte>.json`. L'**empreinte** identifie la config du
        > décodeur (modèle, prompt et sa version, température, topP, maxOutputTokens) — jamais le
        > nombre de décodages, qui change la granularité de R̄ et non le décodeur. **Deux `recovery`
        > d'empreintes différentes ne se comparent pas.**
```

- [ ] **Step 4 : Étendre `Scoring/ScoreCommand.cs`**

Ajouter `using SoClover.Eval.Calibration;` en tête. Dans `ExecuteAsync`, remplacer le bloc `if (args.Get("ledger") is { } ledgerPath)` (lignes 41-62) par :

```csharp
        if (args.Get("ledger") is { } ledgerPath)
        {
            var settings = ComposeSettings(
                run.Manifest, subsetName, metrics.DirectionCount, benchDirectionCount);

            var calibrationPath = args.Get("calibration");
            var calibration = calibrationPath is null
                ? null
                : EvalJson.Deserialize<CalibrationReport>(File.ReadAllText(calibrationPath));

            LedgerWriter.Append(ledgerPath, new LedgerEntry(
                Date: DateOnly.FromDateTime(DateTime.UtcNow),
                RunId: run.Manifest.RunId,
                BenchFile: run.Manifest.BenchFile,
                PromptFile: run.Manifest.PromptFile,
                PromptVersion: run.Manifest.PromptVersion,
                ModelId: run.Manifest.ModelId,
                ModelSnapshotDate: run.Manifest.ModelSnapshotDate,
                Settings: settings,
                Metrics: metrics,
                Status: ResolveStatus(calibration, decoded, calibrationPath ?? "—"),
                Hypothesis: args.Get("hypothesis"),
                Decision: args.Get("decision") ?? "neutre",
                OperatorNotes: ComposeNotes(run.Manifest.OperatorNotes, decoded)));

            Console.WriteLine($"ligne ajoutée au registre : {ledgerPath}");
        }
```

Ajouter la méthode, à côté de `ComposeSettings` :

```csharp
    /// <summary>
    /// Le statut passe à <c>calibré</c> <b>si et seulement si</b> le fichier de calibration
    /// déclare les quatre portes franchies <b>et</b> que son empreinte est celle du
    /// <c>.decoded.jsonl</c> du run scoré.
    /// <para>
    /// Sinon : <b>refus bruyant</b>, jamais de dégradation silencieuse en <c>pré-calibration</c>.
    /// Qui passe le drapeau veut publier une ligne défendable ; produire à la place une ligne mal
    /// étiquetée serait le pire des deux mondes. <b>Sans</b> le drapeau, le comportement est
    /// inchangé.
    /// </para>
    /// </summary>
    internal static string ResolveStatus(
        CalibrationReport? calibration, DecodeContents? decoded, string calibrationPathForMessages)
    {
        if (calibration is null)
            return LedgerWriter.PreCalibrationStatus;

        if (!calibration.AllGatesPassed)
            throw new InvalidOperationException(
                $"{calibrationPathForMessages} déclare « {calibration.Verdict} » : toutes les portes " +
                "ne sont pas franchies, aucune ligne ne peut être publiée comme défendable. " +
                "Retirer --calibration, ou refranchir les portes.");

        if (decoded is null)
            throw new InvalidOperationException(
                "Ce run n'a pas de fichier de décodage : un statut calibré suppose un recovery, " +
                "donc un décodage. Lancer `decode` avant de scorer avec --calibration.");

        var runFingerprint = DecoderFingerprint.FromManifest(decoded.Manifest);
        if (!string.Equals(runFingerprint, calibration.DecoderFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Le run a été décodé par l'empreinte {runFingerprint}, la calibration porte sur " +
                $"{calibration.DecoderFingerprint}. Deux recovery d'empreintes différentes ne se " +
                "comparent pas — re-décoder ce run avec le décodeur calibré (decode --force).");

        return LedgerWriter.CalibratedStatusFor(calibration.DecoderFingerprint);
    }
```

- [ ] **Step 5 : Compléter l'usage dans `Program.cs`**

Dans le bloc `Usage()`, sous la section « Sous-ensemble », ajouter :

```
            Registre (score) :
              --calibration <calibration.json>  statut `calibré` — refuse si une porte est tombée
                                                ou si l'empreinte du décodeur ne concorde pas
```

- [ ] **Step 6 : Exécuter, vérifier le passage**

Run : `dotnet test --filter "FullyQualifiedName~ScoreCalibrationStatusTests"`
Attendu : **9 tests PASS**.

- [ ] **Step 7 : Vérifier que le filet n'a pas bougé**

Run : `dotnet test --filter "FullyQualifiedName~LedgerWriterTests|FullyQualifiedName~ScoreCommandNotesTests"`
Attendu : **PASS**, fichiers de test **non modifiés**. En particulier `Pipe_characters_in_free_text_do_not_break_the_markdown_table` (18 colonnes) et `Row_is_marked_pre_calibration_in_this_cycle`.

- [ ] **Step 8 : Suite complète et build**

Run : `dotnet build` puis `dotnet test`

- [ ] **Step 9 : Commit**

```bash
git add SoClover.Eval/Io/LedgerWriter.cs SoClover.Eval/Scoring/ScoreCommand.cs SoClover.Eval/Program.cs SoClover.Tests/Eval/ScoreCalibrationStatusTests.cs
git commit -m "feat(eval): statut calibre au registre — quatre portes et empreinte concordante"
```

---

## Task 8 : `Analysis/FailureTaxonomy.cs` — la taxonomie chiffrée

Le PRD donne l'**intention** des modes ; cette tâche les rend **calculables**. Chaque direction reçoit **une** étiquette, dans un ordre de priorité écrit — les signatures se recouvrent, et une taxonomie dont l'ordre d'évaluation n'est pas fixé produit des distributions non reproductibles.

> **Ordre retenu : `M2 → M3 → M4 → M1 → M?`** (décision **D9**, seul écart de ce plan à une prescription littérale du design). Le design écrit `M2 → M3 → M1 → M4` ; sous cet ordre, **M4 ne peut jamais sortir** : `R̄ = 0` implique deux mots faux par décodage, et une intersection vide sur ≥ 2 décodages implique ≥ 4 mots faux distincts — la signature M1 est donc toujours satisfaite d'abord. `R̄ = 0` est la condition strictement plus forte : elle passe devant. Un mode structurellement mort dans une taxonomie qui sert à arbitrer des interventions est plus coûteux qu'un ordre corrigé et documenté.

**Files:**
- Create: `SoClover.Eval/Analysis/FailureTaxonomy.cs`
- Test: `SoClover.Tests/Eval/FailureTaxonomyTests.cs`

**Interfaces:**
- Consumes: `BenchContents`, `BenchBoard`, `BenchBoardMapper.{ReferenceWords, AllWords}`, `RunContents`, `DecodeContents`, `ClueDecodeLine`, `BoardDecodeLine`, `BoardGeometry.AllDirections`
- Produces:
  - `FailureModes.{M0,M1,M2,M3,M4,M5,M6,Unclassified}` → `const string`
  - `FailureModes.Label(string mode)` → `string`
  - `DirectionLabel(string BoardId, string Direction, string Mode, double RBar, int ScoredDecodeCount)`
  - `ModeCount(string Mode, string Label, int Count, double Share, bool ActionJustified)`
  - `TaxonomyReport(string RunId, int DirectionCount, int ScorableDirectionCount, int UnscorableDirectionCount, IReadOnlyList<DirectionLabel> Labels, IReadOnlyList<ModeCount> Distribution, int BoardCount, int M6BoardCount, double M6Share, IReadOnlyList<string> M6Boards)`
  - `FailureTaxonomy.{SuccessThreshold, ActionThreshold, DispersionThreshold, ConcentrationThreshold, M6MinBoardMean, M6MinGap}` → constantes
  - `FailureTaxonomy.Compute(BenchContents bench, RunContents run, DecodeContents decoded)` → `TaxonomyReport`
  - `FailureTaxonomy.LabelDirection(IReadOnlyList<string> referenceWords, IReadOnlyList<ClueDecodeLine> decodes)` → `(string Mode, double RBar, int ScoredCount)` (`internal`)

- [ ] **Step 1 : Écrire le test qui échoue**

Créer `SoClover.Tests/Eval/FailureTaxonomyTests.cs` :

```csharp
using SoClover.Eval.Analysis;
using SoClover.Eval.Decoder;
using Xunit;

namespace SoClover.Tests.Eval;

public class FailureTaxonomyTests
{
    private static readonly string[] Reference = ["Chirurgien", "Enfant"];

    private static ClueDecodeLine Decode(int index, params string[] picked) => new(
        Kind: "decode", BoardId: "dev-007", Direction: "Top", DecodeIndex: index,
        Picked: picked, R: picked.Count(Reference.Contains) / 2.0,
        ShuffleSeed: "1", DecodeFailureKind: null, LatencyMs: 100);

    private static ClueDecodeLine Failed(int index) => new(
        Kind: "decode", BoardId: "dev-007", Direction: "Top", DecodeIndex: index,
        Picked: null, R: null, ShuffleSeed: "1", DecodeFailureKind: "unparseable", LatencyMs: 100);

    private static string Mode(params ClueDecodeLine[] decodes) =>
        FailureTaxonomy.LabelDirection(Reference, decodes).Mode;

    // M0 n'est pas un mode d'échec — mais il faut un dénominateur honnête.
    [Fact]
    public void M0_is_a_direction_that_succeeded()
    {
        Assert.Equal(FailureModes.M0, Mode(
            Decode(0, "Chirurgien", "Enfant"),
            Decode(1, "Chirurgien", "Enfant"),
            Decode(2, "Chirurgien", "Miel")));
    }

    [Fact]
    public void The_success_threshold_is_three_quarters()
    {
        Assert.Equal(0.75, FailureTaxonomy.SuccessThreshold);
    }

    // M2, la signature « Hôpital » : l'indice n'attrape qu'une face, TOUJOURS LA MÊME.
    [Fact]
    public void M2_needs_two_half_decodes_missing_the_same_face()
    {
        Assert.Equal(FailureModes.M2, Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Chirurgien", "Route"),
            Decode(2, "Chirurgien", "Vent")));
    }

    [Fact]
    public void Two_half_decodes_missing_different_faces_are_not_M2()
    {
        Assert.NotEqual(FailureModes.M2, Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Enfant", "Route")));
    }

    // M3 : signature CONCENTRÉE — un même distracteur revient.
    [Fact]
    public void M3_is_a_single_distractor_chosen_in_at_least_two_decodes()
    {
        Assert.Equal(FailureModes.M3, Mode(
            Decode(0, "Miel", "Route"),
            Decode(1, "Miel", "Vent"),
            Decode(2, "Miel", "Ciel")));
    }

    // M1 : signature DISPERSÉE — au moins 4 mots non-référence distincts, R̄ ≤ 0,5 mais NON NUL
    // (à R̄ = 0, c'est M4 qui l'emporte : voir D9).
    [Fact]
    public void M1_is_at_least_four_distinct_wrong_words_and_a_low_RBar()
    {
        Assert.Equal(FailureModes.M1, Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Route", "Vent"),
            Decode(2, "Ciel", "Pont")));
    }

    // M4 : R̄ = 0 ET aucun mot commun d'un décodage à l'autre.
    [Fact]
    public void M4_is_zero_recovery_with_no_word_shared_between_decodes()
    {
        Assert.Equal(FailureModes.M4, Mode(
            Decode(0, "Miel", "Route"),
            Decode(1, "Vent", "Ciel"),
            Decode(2, "Pont", "Sable")));
    }

    // D5 : avec un seul décodage, « aucun mot commun » est vide de sens — l'intersection d'un
    // singleton avec lui-même n'est jamais vide. Sans cette garde, M4 absorberait les
    // directions mal décodées.
    [Fact]
    public void A_single_scored_decode_cannot_be_M4()
    {
        Assert.Equal(FailureModes.Unclassified, Mode(Decode(0, "Miel", "Route")));
    }

    // L'ORDRE DE PRIORITÉ, sur un item qui satisfait plusieurs signatures à la fois.
    [Fact]
    public void M2_wins_over_M3_when_both_signatures_hold()
    {
        // Deux décodages à R = 0,5 manquant « Enfant », ET « Miel » choisi deux fois.
        var mode = Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Chirurgien", "Miel"));

        Assert.Equal(FailureModes.M2, mode);
    }

    [Fact]
    public void M3_wins_over_M1_when_both_signatures_hold()
    {
        // « Miel » concentré (2 fois) ET 4 mots faux distincts.
        var mode = Mode(
            Decode(0, "Miel", "Route"),
            Decode(1, "Miel", "Vent"),
            Decode(2, "Ciel", "Pont"));

        Assert.Equal(FailureModes.M3, mode);
    }

    // D9 : M4 s'évalue AVANT M1. Sous l'ordre littéral du design (M1 puis M4), M4 serait
    // STRUCTURELLEMENT INATTEIGNABLE — R̄ = 0 implique 2 mots faux par décodage, et une
    // intersection vide sur ≥ 2 décodages implique ≥ 4 mots faux distincts, donc M1.
    [Fact]
    public void M4_wins_over_M1_because_zero_recovery_is_the_stronger_signature()
    {
        Assert.Equal(FailureModes.M4, Mode(
            Decode(0, "Miel", "Route"),
            Decode(1, "Vent", "Ciel")));
    }

    [Fact]
    public void The_priority_order_is_exposed_and_documented()
    {
        Assert.Equal(
            [FailureModes.M2, FailureModes.M3, FailureModes.M4, FailureModes.M1],
            FailureTaxonomy.PriorityOrder);
    }

    // Une taxonomie qui classe 100 % des items est une taxonomie qui triche.
    [Fact]
    public void An_item_matching_no_signature_is_left_unclassified()
    {
        // R̄ = 0,667 : sous le seuil de réussite, au-dessus de 0,5 (donc pas M1), non nul (pas M4),
        // deux faces manquées différentes (pas M2), aucun distracteur répété (pas M3).
        Assert.Equal(FailureModes.Unclassified, Mode(
            Decode(0, "Chirurgien", "Miel"),
            Decode(1, "Enfant", "Route"),
            Decode(2, "Chirurgien", "Enfant")));
    }

    // D6 : une direction sans aucun décodage exploitable n'est pas un mode d'échec sémantique.
    [Fact]
    public void A_direction_without_a_single_scored_decode_is_unclassified_and_counted_apart()
    {
        var (mode, rbar, scored) = FailureTaxonomy.LabelDirection(Reference, [Failed(0), Failed(1)]);

        Assert.Equal(FailureModes.Unclassified, mode);
        Assert.Equal(0.0, rbar);
        Assert.Equal(0, scored);
    }

    // M5 reste MANUEL, et c'est une décision, pas un oubli : le harnais n'embarque aucune
    // ressource de fréquence lexicale, et un proxy inventé donnerait une fausse rigueur.
    [Fact]
    public void M5_is_never_produced_automatically()
    {
        var everyShape = new[]
        {
            Mode(Decode(0, "Chirurgien", "Enfant")),
            Mode(Decode(0, "Chirurgien", "Miel"), Decode(1, "Chirurgien", "Route")),
            Mode(Decode(0, "Miel", "Route"), Decode(1, "Vent", "Ciel")),
        };

        Assert.DoesNotContain(FailureModes.M5, everyShape);
    }

    [Fact]
    public void M5_still_has_a_label_for_the_hand_written_sample()
    {
        Assert.False(string.IsNullOrWhiteSpace(FailureModes.Label(FailureModes.M5)));
    }
}
```

- [ ] **Step 2 : Exécuter, vérifier l'échec**

Run : `dotnet test --filter "FullyQualifiedName~FailureTaxonomyTests"`
Attendu : **échec de compilation**.

- [ ] **Step 3 : Écrire `Analysis/FailureTaxonomy.cs`**

```csharp
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Runner;

namespace SoClover.Eval.Analysis;

/// <summary>
/// Vocabulaire <b>fermé</b> des modes d'échec. Les codes viennent du PRD ; les libellés sont
/// affichés dans le rapport et dans l'échantillon relu à la main.
/// </summary>
public static class FailureModes
{
    public const string M0 = "M0";
    public const string M1 = "M1";
    public const string M2 = "M2";
    public const string M3 = "M3";
    public const string M4 = "M4";
    public const string M5 = "M5";
    public const string M6 = "M6";

    /// <summary>Une taxonomie qui classe 100 % des items est une taxonomie qui triche.</summary>
    public const string Unclassified = "M?";

    public static readonly IReadOnlyList<string> All = [M0, M1, M2, M3, M4, M5, M6, Unclassified];

    public static string Label(string mode) => mode switch
    {
        M0 => "réussi",
        M1 => "trop générique",
        M2 => "n'attrape qu'une face",
        M3 => "collision avec un distracteur",
        M4 => "relation trop indirecte",
        M5 => "jargon / mot rare (manuel)",
        M6 => "collision inter-directions (board)",
        Unclassified => "non classé",
        _ => mode,
    };

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
}

public sealed record DirectionLabel(
    string BoardId, string Direction, string Mode, double RBar, int ScoredDecodeCount);

public sealed record ModeCount(string Mode, string Label, int Count, double Share, bool ActionJustified);

public sealed record TaxonomyReport(
    string RunId,
    int DirectionCount,
    int ScorableDirectionCount,
    int UnscorableDirectionCount,
    IReadOnlyList<DirectionLabel> Labels,
    IReadOnlyList<ModeCount> Distribution,
    int BoardCount,
    int M6BoardCount,
    double M6Share,
    IReadOnlyList<string> M6Boards);

/// <summary>
/// Taxonomie chiffrée des modes d'échec. Chaque direction reçoit <b>une</b> étiquette.
/// <para>
/// <b>L'ordre de priorité est déterministe et documenté</b> : <c>M2 → M3 → M4 → M1 → M?</c>. Les
/// signatures se recouvrent — une direction peut être à la fois dispersée et concentrée sur un
/// mot — et une taxonomie dont l'ordre d'évaluation n'est pas écrit produit des distributions non
/// reproductibles.
/// </para>
/// <para>
/// <c>M6</c> est compté <b>séparément, en boards</b>, jamais mélangé à la distribution par
/// direction : ce n'est pas la même unité. <c>M5</c> n'est <b>jamais</b> produit
/// automatiquement — voir <see cref="LabelDirection"/>.
/// </para>
/// </summary>
public static class FailureTaxonomy
{
    /// <summary>Au-dessus, la direction est réussie (<c>M0</c>).</summary>
    public const double SuccessThreshold = 0.75;

    /// <summary>« ≥ 5 % → intervention justifiée » / « &lt; 5 % → aucune ligne de prompt ».</summary>
    public const double ActionThreshold = 0.05;

    /// <summary><c>M1</c> : mots faux <b>dispersés</b>.</summary>
    public const int DispersionThreshold = 4;

    /// <summary><c>M2</c> et <c>M3</c> : signature <b>concentrée</b>, répétée.</summary>
    public const int ConcentrationThreshold = 2;

    public const double M6MinBoardMean = 0.50;
    public const double M6MinGap = 0.20;

    /// <summary>
    /// L'ordre d'évaluation, exposé pour être testable et lisible dans le rapport.
    /// <para>
    /// <b>M4 précède M1</b>, contrairement à l'ordre littéral du design. Sous l'ordre
    /// <c>M1 → M4</c>, M4 serait <b>structurellement inatteignable</b> : R̄ = 0 implique deux mots
    /// faux par décodage, et une intersection vide sur ≥ 2 décodages implique ≥ 4 mots faux
    /// distincts — donc M1 à tous les coups. R̄ = 0 est la condition strictement plus forte : elle
    /// passe devant.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<string> PriorityOrder =
        [FailureModes.M2, FailureModes.M3, FailureModes.M4, FailureModes.M1];

    public static TaxonomyReport Compute(BenchContents bench, RunContents run, DecodeContents decoded)
    {
        var decodesByItem = decoded.ClueDecodes
            .GroupBy(d => (d.BoardId, d.Direction))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ClueDecodeLine>)g.OrderBy(d => d.DecodeIndex).ToList());

        var boardPositionsByBoard = decoded.BoardDecodes
            .Where(b => b.DecodeFailureKind is null && b.BoardPositions is not null)
            .GroupBy(b => b.BoardId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().BoardPositions!.Value, StringComparer.Ordinal);

        var labels = new List<DirectionLabel>();
        var m6Boards = new List<string>();

        foreach (var board in bench.Boards)
        {
            var perDirection = new List<double>(4);

            foreach (var direction in BoardGeometry.AllDirections)
            {
                var key = (board.BoardId, direction.ToString());
                var decodes = decodesByItem.TryGetValue(key, out var d) ? d : [];
                var reference = BenchBoardMapper.ReferenceWords(board, direction);

                var (mode, rBar, scored) = LabelDirection(reference, decodes);
                labels.Add(new DirectionLabel(board.BoardId, direction.ToString(), mode, rBar, scored));
                perDirection.Add(rBar);
            }

            // M6 : signature AU NIVEAU BOARD. Les indices marchent un par un, mais mis ensemble
            // ils se disputent les mêmes mots.
            if (boardPositionsByBoard.TryGetValue(board.BoardId, out var boardPositions)
                && perDirection.Average() >= M6MinBoardMean
                && perDirection.Average() - boardPositions >= M6MinGap)
                m6Boards.Add(board.BoardId);
        }

        var scorable = labels.Count(l => l.ScoredDecodeCount > 0);
        var distribution = FailureModes.All
            .Where(m => m != FailureModes.M6)
            .Select(m =>
            {
                var count = labels.Count(l => l.Mode == m);
                var share = labels.Count == 0 ? 0.0 : count / (double)labels.Count;
                return new ModeCount(m, FailureModes.Label(m), count, share, share >= ActionThreshold);
            })
            .ToList()
            .AsReadOnly();

        return new TaxonomyReport(
            RunId: run.Manifest.RunId,
            DirectionCount: labels.Count,
            ScorableDirectionCount: scorable,
            UnscorableDirectionCount: labels.Count - scorable,
            Labels: labels.AsReadOnly(),
            Distribution: distribution,
            BoardCount: bench.Boards.Count,
            M6BoardCount: m6Boards.Count,
            M6Share: bench.Boards.Count == 0 ? 0.0 : m6Boards.Count / (double)bench.Boards.Count,
            M6Boards: m6Boards.AsReadOnly());
    }

    /// <summary>
    /// L'étiquette d'une direction, et rien d'autre : ni <c>M5</c> (non détectable
    /// automatiquement — le harnais n'embarque aucune ressource de fréquence lexicale, et un
    /// proxy inventé donnerait une fausse impression de rigueur), ni <c>M6</c> (unité board).
    /// </summary>
    internal static (string Mode, double RBar, int ScoredCount) LabelDirection(
        IReadOnlyList<string> referenceWords, IReadOnlyList<ClueDecodeLine> decodes)
    {
        var scored = decodes
            .Where(d => d.DecodeFailureKind is null && d.R is not null && d.Picked is not null)
            .ToList();

        // D6 : ni indice valide, ni décodage exploitable. Ce n'est pas un mode d'échec
        // sémantique — le rapport le comptera à part.
        if (scored.Count == 0)
            return (FailureModes.Unclassified, 0.0, 0);

        var rBar = scored.Average(d => d.R!.Value);
        if (rBar >= SuccessThreshold)
            return (FailureModes.M0, rBar, scored.Count);

        // M2 — « Hôpital » : ≥ 2 décodages à R = 0,5 ET LA MÊME FACE MANQUÉE à chaque fois.
        var halves = scored.Where(d => d.R!.Value == 0.5).ToList();
        if (halves.Count >= ConcentrationThreshold)
        {
            var missed = halves
                .Select(d => referenceWords.Single(w => !d.Picked!.Contains(w)))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (missed.Count == 1)
                return (FailureModes.M2, rBar, scored.Count);
        }

        var wrongPerDecode = scored
            .Select(d => d.Picked!.Where(w => !referenceWords.Contains(w)).ToList())
            .ToList();
        var wrongCounts = wrongPerDecode
            .SelectMany(w => w)
            .GroupBy(w => w, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        // M3 — collision : un même mot non-référence choisi dans ≥ 2 décodages (CONCENTRÉ).
        if (wrongCounts.Values.Any(c => c >= ConcentrationThreshold))
            return (FailureModes.M3, rBar, scored.Count);

        // M4 — relation trop indirecte : R̄ = 0 ET aucun mot commun aux paires choisies.
        // D5 : au moins deux décodages, sinon « aucun mot commun » est vide de sens.
        // D9 : AVANT M1 — sous l'ordre inverse, M4 serait inatteignable (cf. PriorityOrder).
        if (rBar == 0.0 && scored.Count >= 2)
        {
            var common = wrongPerDecode
                .Select(w => (IEnumerable<string>)w)
                .Aggregate((a, b) => a.Intersect(b, StringComparer.Ordinal))
                .ToList();

            if (common.Count == 0)
                return (FailureModes.M4, rBar, scored.Count);
        }

        // M1 — trop générique : R̄ ≤ 0,5 ET mots faux DISPERSÉS.
        if (rBar <= 0.5 && wrongCounts.Count >= DispersionThreshold)
            return (FailureModes.M1, rBar, scored.Count);

        return (FailureModes.Unclassified, rBar, scored.Count);
    }
}
```

- [ ] **Step 4 : Exécuter, vérifier le passage**

Run : `dotnet test --filter "FullyQualifiedName~FailureTaxonomyTests"`
Attendu : **17 tests PASS**.

Si `M1_wins_over_M4_when_both_signatures_hold` échoue en rendant `M4`, l'ordre a été inversé : `M1` s'évalue **avant** `M4`, conformément à `PriorityOrder`.

- [ ] **Step 5 : Suite complète et build**

Run : `dotnet build` puis `dotnet test`

- [ ] **Step 6 : Commit**

```bash
git add SoClover.Eval/Analysis/FailureTaxonomy.cs SoClover.Tests/Eval/FailureTaxonomyTests.cs
git commit -m "feat(eval): taxonomie des modes d'echec — signatures M0-M6 et ordre de priorite"
```

---

## Task 9 : `AnalysisSample` + verbe `analyze`

**Valider l'étiquetage plutôt que le croire.** Sans cette tâche, on publie une distribution d'étiquettes que *personne n'a jamais vérifiée* — et une taxonomie fausse est plus coûteuse qu'aucune taxonomie, puisqu'elle oriente les interventions.

**Files:**
- Create: `SoClover.Eval/Analysis/AnalysisSample.cs`
- Create: `SoClover.Eval/Analysis/AnalyzeCommand.cs`
- Modify: `SoClover.Eval/Program.cs` (verbe `analyze` + usage)
- Modify: `.gitignore` (`eval/analysis/*.taxonomy.json`)
- Test: `SoClover.Tests/Eval/AnalysisSampleTests.cs`

**Interfaces:**
- Consumes: `TaxonomyReport`, `DirectionLabel`, `FailureModes`, `FailureTaxonomy.Compute` (T8) ; `BenchContents`, `BenchBoardMapper`, `RunContents`, `DecodeContents` ; `Xoshiro256SS`
- Produces:
  - `SampleItem(string BoardId, string Direction, string Clue, IReadOnlyList<string> ReferenceWords, IReadOnlyList<string> BoardWords, IReadOnlyList<string> DecodeLines, double RBar, string AutoMode, string? HumanMode)`
  - `ConfusionRow(string AutoMode, string HumanMode, int Count)`
  - `ConfusionMatrix(int Total, double Agreement, IReadOnlyList<ConfusionRow> Rows)`
  - `AnalysisSample.{DefaultSize, ReviewAgreementThreshold}` → `const`
  - `AnalysisSample.Draw(BenchContents, RunContents, DecodeContents, TaxonomyReport, int size, long seed)` → `IReadOnlyList<SampleItem>`
  - `AnalysisSample.Render(string runId, long seed, IReadOnlyList<SampleItem> items)` → `string`
  - `AnalysisSample.Parse(string markdown)` → `IReadOnlyList<SampleItem>`
  - `AnalysisSample.Review(IReadOnlyList<SampleItem> items)` → `ConfusionMatrix`
  - `AnalysisSample.PathFor(string directory, string runId)` / `TaxonomyPathFor(...)` → `string`
  - `AnalyzeCommand.ExecuteAsync(Args, CancellationToken)` → `Task<int>`

- [ ] **Step 1 : Écrire le test qui échoue**

Créer `SoClover.Tests/Eval/AnalysisSampleTests.cs` :

```csharp
using SoClover.Eval.Analysis;
using Xunit;

namespace SoClover.Tests.Eval;

public class AnalysisSampleTests
{
    private static SampleItem Item(
        string boardId = "dev-007", string direction = "Top", string clue = "Pédiatre",
        string autoMode = FailureModes.M2, string? humanMode = null, double rBar = 0.5) => new(
        BoardId: boardId,
        Direction: direction,
        Clue: clue,
        ReferenceWords: ["Chirurgien", "Enfant"],
        BoardWords: ["Chirurgien", "Enfant", "Île", "Forêt", "Vague", "Miel", "Tambour", "Ciel",
                     "Route", "Plage", "Sable", "Rocher", "Oiseau", "Montagne", "Vent", "Pont"],
        DecodeLines: ["Chirurgien + Miel   (R = 0,5)", "Chirurgien + Route   (R = 0,5)",
                      "— (échec de format : unparseable)"],
        RBar: rBar,
        AutoMode: autoMode,
        HumanMode: humanMode);

    // Un fichier LISIBLE PAR UN HUMAIN, pas un JSON : c'est la condition pour que les 20 items
    // soient réellement lus.
    [Fact]
    public void The_rendered_sample_shows_everything_needed_to_judge_an_item()
    {
        var markdown = AnalysisSample.Render("run-x", 4242, [Item()]);

        Assert.Contains("run-x", markdown);
        Assert.Contains("4242", markdown);
        Assert.Contains("Pédiatre", markdown);
        Assert.Contains("Chirurgien", markdown);
        Assert.Contains("Montagne", markdown);          // les 16 mots
        Assert.Contains("Chirurgien + Miel", markdown); // les décodages obtenus
        Assert.Contains(FailureModes.M2, markdown);     // l'étiquette proposée
        Assert.Contains("étiquette humaine :", markdown);
    }

    [Fact]
    public void The_rendered_sample_lists_the_closed_vocabulary_of_modes()
    {
        var markdown = AnalysisSample.Render("run-x", 1, [Item()]);

        Assert.All(FailureModes.All, m => Assert.Contains(m, markdown));
    }

    [Fact]
    public void Round_trips_through_render_and_parse()
    {
        var items = new[]
        {
            Item(boardId: "dev-007", direction: "Top", clue: "Pédiatre", autoMode: FailureModes.M2),
            Item(boardId: "dev-012", direction: "Left", clue: "Mer", autoMode: FailureModes.M1),
        };

        var parsed = AnalysisSample.Parse(AnalysisSample.Render("run-x", 7, items));

        Assert.Equal(2, parsed.Count);
        Assert.Equal("dev-012", parsed[1].BoardId);
        Assert.Equal("Left", parsed[1].Direction);
        Assert.Equal("Mer", parsed[1].Clue);
        Assert.Equal(FailureModes.M1, parsed[1].AutoMode);
        Assert.Null(parsed[1].HumanMode);
    }

    [Fact]
    public void Parses_the_human_label_once_the_operator_has_filled_it()
    {
        var markdown = AnalysisSample.Render("run-x", 7, [Item()])
            .Replace("- étiquette humaine :", "- étiquette humaine : M3");

        Assert.Equal(FailureModes.M3, AnalysisSample.Parse(markdown).Single().HumanMode);
    }

    // M5 est posé À LA MAIN sur l'échantillon lu : c'est la seule voie par laquelle il entre.
    [Fact]
    public void A_hand_written_M5_is_accepted()
    {
        var markdown = AnalysisSample.Render("run-x", 7, [Item()])
            .Replace("- étiquette humaine :", "- étiquette humaine : M5");

        Assert.Equal(FailureModes.M5, AnalysisSample.Parse(markdown).Single().HumanMode);
    }

    [Fact]
    public void Refuses_a_human_label_outside_the_closed_vocabulary()
    {
        var markdown = AnalysisSample.Render("run-x", 7, [Item()])
            .Replace("- étiquette humaine :", "- étiquette humaine : M9");

        var ex = Assert.Throws<ArgumentException>(() => AnalysisSample.Parse(markdown));
        Assert.Contains("M9", ex.Message);
    }

    // ---- Matrice de confusion --------------------------------------------------

    [Fact]
    public void Review_reports_the_agreement_between_the_automatic_and_the_human_labels()
    {
        var items = new[]
        {
            Item(autoMode: FailureModes.M2, humanMode: FailureModes.M2),
            Item(autoMode: FailureModes.M2, humanMode: FailureModes.M2, boardId: "dev-001"),
            Item(autoMode: FailureModes.M1, humanMode: FailureModes.M3, boardId: "dev-002"),
            Item(autoMode: FailureModes.M4, humanMode: FailureModes.M4, boardId: "dev-003"),
        };

        var matrix = AnalysisSample.Review(items);

        Assert.Equal(4, matrix.Total);
        Assert.Equal(0.75, matrix.Agreement, precision: 10);
        Assert.Equal(2, matrix.Rows.Single(r => r.AutoMode == FailureModes.M2
                                                && r.HumanMode == FailureModes.M2).Count);
        Assert.Equal(1, matrix.Rows.Single(r => r.AutoMode == FailureModes.M1
                                                && r.HumanMode == FailureModes.M3).Count);
    }

    [Fact]
    public void The_review_threshold_is_seventy_percent()
    {
        Assert.Equal(0.70, AnalysisSample.ReviewAgreementThreshold);
    }

    // Une relecture partielle n'est pas une relecture : le refus nomme le nombre de lignes vides.
    [Fact]
    public void Review_refuses_a_sample_whose_human_labels_are_missing()
    {
        var items = new[]
        {
            Item(humanMode: FailureModes.M2),
            Item(humanMode: null, boardId: "dev-001"),
            Item(humanMode: null, boardId: "dev-002"),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => AnalysisSample.Review(items));

        Assert.Contains("2", ex.Message);
    }

    // ---- Tirage ----------------------------------------------------------------

    [Fact]
    public void The_draw_is_reproducible_for_a_fixed_seed()
    {
        var (bench, run, decoded, taxonomy) = AnalysisFixture.Build();

        var first = AnalysisSample.Draw(bench, run, decoded, taxonomy, size: 5, seed: 99);
        var second = AnalysisSample.Draw(bench, run, decoded, taxonomy, size: 5, seed: 99);

        Assert.Equal(
            first.Select(i => (i.BoardId, i.Direction)),
            second.Select(i => (i.BoardId, i.Direction)));
    }

    [Fact]
    public void A_different_seed_draws_a_different_sample()
    {
        var (bench, run, decoded, taxonomy) = AnalysisFixture.Build();

        var a = AnalysisSample.Draw(bench, run, decoded, taxonomy, size: 5, seed: 1);
        var b = AnalysisSample.Draw(bench, run, decoded, taxonomy, size: 5, seed: 2);

        Assert.NotEqual(
            a.Select(i => (i.BoardId, i.Direction)).ToList(),
            b.Select(i => (i.BoardId, i.Direction)).ToList());
    }

    // On tire parmi les ÉCHECS : lire 20 réussites n'apprendrait rien sur les modes d'échec.
    [Fact]
    public void The_draw_only_picks_failing_directions()
    {
        var (bench, run, decoded, taxonomy) = AnalysisFixture.Build();

        var sample = AnalysisSample.Draw(bench, run, decoded, taxonomy, size: 10, seed: 5);

        Assert.All(sample, i => Assert.True(i.RBar < FailureTaxonomy.SuccessThreshold));
        Assert.DoesNotContain(sample, i => i.AutoMode == FailureModes.M0);
    }

    [Fact]
    public void The_draw_is_capped_by_the_number_of_available_failures()
    {
        var (bench, run, decoded, taxonomy) = AnalysisFixture.Build();

        var sample = AnalysisSample.Draw(bench, run, decoded, taxonomy, size: 10_000, seed: 5);

        Assert.Equal(taxonomy.Labels.Count(l => l.Mode != FailureModes.M0), sample.Count);
    }
}
```

> **`AnalysisFixture`** : petite fabrique à écrire dans le même fichier (ou dans `Helpers/`), construisant un banc de 5 boards via `HumanTestData.Bench(5)`, un run via `HumanTestData.Run(bench, "run-x", "clue")`, et un `DecodeContents` dont **la moitié des directions** réussit (`R = 1`) et l'autre échoue (`R = 0` avec des mots faux tous distincts, donc `M4`), puis `FailureTaxonomy.Compute` dessus. Écrire cette fabrique **avant** les tests de tirage : sans elle, ils ne compilent pas.

- [ ] **Step 2 : Exécuter, vérifier l'échec**

Run : `dotnet test --filter "FullyQualifiedName~AnalysisSampleTests"`
Attendu : **échec de compilation**.

- [ ] **Step 3 : Écrire `Analysis/AnalysisSample.cs`**

```csharp
using System.Globalization;
using System.Text;
using SoClover.Domain;
using SoClover.Eval.Bench;
using SoClover.Eval.Decoder;
using SoClover.Eval.Runner;

namespace SoClover.Eval.Analysis;

public sealed record SampleItem(
    string BoardId,
    string Direction,
    string Clue,
    IReadOnlyList<string> ReferenceWords,
    IReadOnlyList<string> BoardWords,
    IReadOnlyList<string> DecodeLines,
    double RBar,
    string AutoMode,
    string? HumanMode);

public sealed record ConfusionRow(string AutoMode, string HumanMode, int Count);

public sealed record ConfusionMatrix(int Total, double Agreement, IReadOnlyList<ConfusionRow> Rows);

/// <summary>
/// Échantillon seedé d'échecs, rendu <b>lisible par un humain</b>, et relecture de l'étiquetage.
/// <para>
/// C'est la matérialisation du « lire ~20 échecs, les ranger en modes d'échec, compter » de la
/// boucle du PRD. Sans elle, on publie une distribution d'étiquettes que <b>personne n'a jamais
/// vérifiée</b> — et une taxonomie fausse est plus coûteuse qu'aucune taxonomie, puisqu'elle
/// oriente les interventions.
/// </para>
/// </summary>
public static class AnalysisSample
{
    public const int DefaultSize = 20;

    /// <summary>Seuil indicatif ; en dessous, les seuils des signatures sont à revoir — de façon datée.</summary>
    public const double ReviewAgreementThreshold = 0.70;

    private const string ClueKey = "- indice";
    private const string AutoKey = "- étiquette auto";
    private const string HumanKey = "- étiquette humaine";

    public static string PathFor(string directory, string runId) =>
        Path.Combine(directory, $"{runId}.sample.md");

    public static string TaxonomyPathFor(string directory, string runId) =>
        Path.Combine(directory, $"{runId}.taxonomy.json");

    public static IReadOnlyList<SampleItem> Draw(
        BenchContents bench, RunContents run, DecodeContents decoded,
        TaxonomyReport taxonomy, int size, long seed)
    {
        var boards = bench.Boards.ToDictionary(b => b.BoardId, StringComparer.Ordinal);

        var clues = run.Attempts
            .Where(a => a.Valid && a.Clue is not null)
            .GroupBy(a => (a.BoardId, a.Direction))
            .ToDictionary(g => g.Key, g => g.Last().Clue!);

        var decodesByItem = decoded.ClueDecodes
            .GroupBy(d => (d.BoardId, d.Direction))
            .ToDictionary(g => g.Key, g => g.OrderBy(d => d.DecodeIndex).ToList());

        // On tire parmi les ÉCHECS : lire 20 réussites n'apprendrait rien sur les modes d'échec.
        var candidates = taxonomy.Labels
            .Where(l => l.Mode != FailureModes.M0)
            .OrderBy(l => l.BoardId, StringComparer.Ordinal)
            .ThenBy(l => l.Direction, StringComparer.Ordinal)
            .ToList();

        new Xoshiro256SS(seed).Shuffle(candidates);

        return candidates
            .Take(Math.Min(size, candidates.Count))
            .Select(l =>
            {
                var board = boards[l.BoardId];
                var direction = Enum.Parse<Direction>(l.Direction);
                var key = (l.BoardId, l.Direction);
                var lines = decodesByItem.TryGetValue(key, out var d) ? d : [];

                return new SampleItem(
                    BoardId: l.BoardId,
                    Direction: l.Direction,
                    Clue: clues.TryGetValue(key, out var clue) ? clue : "— (aucun indice valide)",
                    ReferenceWords: BenchBoardMapper.ReferenceWords(board, direction),
                    BoardWords: BenchBoardMapper.AllWords(board),
                    DecodeLines: lines.Select(Describe).ToList().AsReadOnly(),
                    RBar: l.RBar,
                    AutoMode: l.Mode,
                    HumanMode: null);
            })
            .ToList()
            .AsReadOnly();
    }

    private static string Describe(ClueDecodeLine line) =>
        line.DecodeFailureKind is { } failure || line.Picked is null
            ? $"— (échec de format : {line.DecodeFailureKind ?? "inconnu"})"
            : $"{string.Join(" + ", line.Picked)}   (R = {Fr(line.R ?? 0)})";

    public static string Render(string runId, long seed, IReadOnlyList<SampleItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Échantillon d'échecs — {runId}");
        sb.AppendLine();
        sb.AppendLine($"> seed **{seed}** — {items.Count} item(s) tirés parmi les directions dont");
        sb.AppendLine($"> R̄ < {Fr(FailureTaxonomy.SuccessThreshold)}. Renseigner `étiquette humaine :` pour");
        sb.AppendLine("> **chaque** item, puis relancer :");
        sb.AppendLine(">");
        sb.AppendLine($"> `analyze --review eval/analysis/{runId}.sample.md`");
        sb.AppendLine(">");
        sb.AppendLine("> Vocabulaire fermé — " + string.Join(" · ",
            FailureModes.All.Select(m => $"`{m}` {FailureModes.Label(m)}")));
        sb.AppendLine(">");
        sb.AppendLine("> `M5` ne sort **jamais** automatiquement : c'est ici, et seulement ici, qu'il se pose.");
        sb.AppendLine();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            sb.AppendLine($"## {i + 1} — {item.BoardId} / {item.Direction}");
            sb.AppendLine();
            sb.AppendLine($"{ClueKey} : {item.Clue}");
            sb.AppendLine($"- paire cible : {string.Join(" + ", item.ReferenceWords)}");
            sb.AppendLine($"- R̄ : {Fr(item.RBar)}");
            sb.AppendLine($"- 16 mots : {string.Join(", ", item.BoardWords)}");
            foreach (var (line, index) in item.DecodeLines.Select((l, n) => (l, n)))
                sb.AppendLine($"- décodage {index} : {line}");
            sb.AppendLine($"{AutoKey} : {item.AutoMode}  ({FailureModes.Label(item.AutoMode)})");
            sb.AppendLine($"{HumanKey} :{(item.HumanMode is null ? string.Empty : " " + item.HumanMode)}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Relit le fichier rempli. Ne reconstruit que ce dont la matrice de confusion a besoin —
    /// identifiant de l'item, indice, étiquette automatique, étiquette humaine.
    /// </summary>
    public static IReadOnlyList<SampleItem> Parse(string markdown)
    {
        var items = new List<SampleItem>();
        string? boardId = null, direction = null, clue = null, autoMode = null, humanMode = null;
        var decodeLines = new List<string>();

        void Flush()
        {
            if (boardId is null) return;
            items.Add(new SampleItem(
                boardId, direction ?? string.Empty, clue ?? string.Empty,
                [], [], decodeLines.ToList().AsReadOnly(), 0.0,
                autoMode ?? FailureModes.Unclassified, humanMode));
        }

        foreach (var raw in markdown.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            var line = raw.Trim();

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                boardId = direction = clue = autoMode = humanMode = null;
                decodeLines.Clear();

                // « ## 3 — dev-007 / Top »
                var header = line[3..].Split('—', 2).Last().Trim();
                var parts = header.Split('/', 2, StringSplitOptions.TrimEntries);
                boardId = parts[0];
                direction = parts.Length > 1 ? parts[1] : string.Empty;
                continue;
            }

            if (line.StartsWith(ClueKey, StringComparison.Ordinal)) clue = ValueOf(line);
            else if (line.StartsWith(AutoKey, StringComparison.Ordinal)) autoMode = ModeOf(ValueOf(line));
            else if (line.StartsWith(HumanKey, StringComparison.Ordinal))
            {
                var value = ValueOf(line);
                humanMode = value.Length == 0 ? null : ModeOf(value);
            }
            else if (line.StartsWith("- décodage ", StringComparison.Ordinal)) decodeLines.Add(ValueOf(line));
        }

        Flush();
        return items.AsReadOnly();
    }

    private static string ValueOf(string line)
    {
        var separator = line.IndexOf(':');
        return separator < 0 ? string.Empty : line[(separator + 1)..].Trim();
    }

    /// <summary>Le premier mot de la valeur : « M2  (n'attrape qu'une face) » → « M2 ».</summary>
    private static string ModeOf(string value)
    {
        var mode = value.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        if (!FailureModes.IsKnown(mode))
            throw new ArgumentException(
                $"Étiquette « {mode} » hors du vocabulaire fermé : {string.Join(", ", FailureModes.All)}.");

        return mode;
    }

    public static ConfusionMatrix Review(IReadOnlyList<SampleItem> items)
    {
        var missing = items.Count(i => i.HumanMode is null);
        if (missing > 0)
            throw new InvalidOperationException(
                $"{missing} item(s) sur {items.Count} n'ont pas d'étiquette humaine. Une relecture " +
                "partielle n'est pas une relecture : remplir toutes les lignes « étiquette humaine : ».");

        var rows = items
            .GroupBy(i => (i.AutoMode, i.HumanMode))
            .OrderBy(g => g.Key.AutoMode, StringComparer.Ordinal)
            .ThenBy(g => g.Key.HumanMode, StringComparer.Ordinal)
            .Select(g => new ConfusionRow(g.Key.AutoMode, g.Key.HumanMode!, g.Count()))
            .ToList()
            .AsReadOnly();

        var agreeing = items.Count(i => i.AutoMode == i.HumanMode);
        return new ConfusionMatrix(
            items.Count,
            items.Count == 0 ? 0.0 : agreeing / (double)items.Count,
            rows);
    }

    private static string Fr(double value) =>
        value.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"));
}
```

- [ ] **Step 4 : Exécuter, vérifier le passage**

Run : `dotnet test --filter "FullyQualifiedName~AnalysisSampleTests"`
Attendu : **13 tests PASS**.

- [ ] **Step 5 : Écrire `Analysis/AnalyzeCommand.cs`**

```csharp
using System.Globalization;
using SoClover.Eval.Bench;
using SoClover.Eval.Cli;
using SoClover.Eval.Io;

namespace SoClover.Eval.Analysis;

/// <summary>
/// Verbe <c>analyze</c> : taxonomie chiffrée d'un run décodé, échantillon relu à la main, et
/// matrice de confusion auto ↔ humain. <b>Aucun appel LLM.</b>
/// </summary>
public static class AnalyzeCommand
{
    public static Task<int> ExecuteAsync(Args args, CancellationToken ct)
    {
        var outDirectory = args.Get("out") ?? Path.Combine("eval", "analysis");

        if (args.Get("review") is { } reviewPath)
            return Task.FromResult(Review(reviewPath));

        var runPath = args.Require("run");
        var run = RunFile.Read(runPath);
        var bench = BenchFile.Read(run.Manifest.BenchFile);

        // La taxonomie se lit sur les DÉCODAGES, pas sur les indices.
        var decodedPath = args.Get("decoded") ?? DecodeFile.PathFor(runPath);
        var decoded = DecodeFile.ReadOrNull(decodedPath)
            ?? throw new InvalidOperationException(
                $"{decodedPath} est absent : la taxonomie se lit sur les décodages, pas sur les " +
                "indices. Lancer `decode` d'abord.");

        var taxonomy = FailureTaxonomy.Compute(bench, run, decoded);

        Directory.CreateDirectory(outDirectory);
        var taxonomyPath = AnalysisSample.TaxonomyPathFor(outDirectory, taxonomy.RunId);
        File.WriteAllText(taxonomyPath, EvalJson.Serialize(taxonomy));

        Print(taxonomy);
        Console.WriteLine($"taxonomie écrite : {taxonomyPath}");

        if (args.Has("sample"))
        {
            var size = args.GetInt("sample", AnalysisSample.DefaultSize);
            var seed = args.GetLong("seed", 0);
            if (seed == 0) throw new ArgumentException("--seed est requis et doit être non nul.");

            var items = AnalysisSample.Draw(bench, run, decoded, taxonomy, size, seed);
            var samplePath = AnalysisSample.PathFor(outDirectory, taxonomy.RunId);

            if (File.Exists(samplePath) && !args.Has("force"))
                throw new InvalidOperationException(
                    $"{samplePath} existe déjà. Le réécrire perdrait les étiquettes humaines déjà " +
                    "posées : passer --force en connaissance de cause.");

            File.WriteAllText(samplePath, AnalysisSample.Render(taxonomy.RunId, seed, items));

            Console.WriteLine();
            Console.WriteLine($"échantillon écrit : {samplePath}");
            Console.WriteLine($"  {items.Count} item(s), seed {seed}. Remplir « étiquette humaine : » " +
                              "pour chacun, puis :");
            Console.WriteLine($"  analyze --review {samplePath.Replace('\\', '/')}");
        }

        return Task.FromResult(0);
    }

    private static int Review(string path)
    {
        static string N(double v) => v.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"));

        var items = AnalysisSample.Parse(File.ReadAllText(path));
        var matrix = AnalysisSample.Review(items);

        Console.WriteLine();
        Console.WriteLine($"relecture de {path} — {matrix.Total} item(s)");
        Console.WriteLine();
        Console.WriteLine("matrice de confusion  auto → humain");
        foreach (var row in matrix.Rows)
            Console.WriteLine($"  {row.AutoMode,-4} → {row.HumanMode,-4}   {row.Count,4}" +
                              (row.AutoMode == row.HumanMode ? "   ✓" : string.Empty));
        Console.WriteLine();
        Console.WriteLine($"accord auto ↔ humain   {N(matrix.Agreement)}   " +
                          $"(seuil indicatif {N(AnalysisSample.ReviewAgreementThreshold)})");

        if (matrix.Agreement < AnalysisSample.ReviewAgreementThreshold)
            Console.WriteLine(
                "  ⚠ sous le seuil : les seuils des signatures sont à revoir, et la révision doit " +
                "être DATÉE. Une taxonomie fausse oriente les interventions — elle coûte plus cher " +
                "qu'aucune taxonomie.");

        Console.WriteLine();
        return 0;
    }

    private static void Print(TaxonomyReport t)
    {
        static string N(double v) => v.ToString("0.000", CultureInfo.GetCultureInfo("fr-FR"));

        Console.WriteLine();
        Console.WriteLine($"taxonomie — {t.RunId}");
        Console.WriteLine($"  directions             {t.DirectionCount}");
        Console.WriteLine($"  dont exploitables      {t.ScorableDirectionCount}");
        if (t.UnscorableDirectionCount > 0)
            Console.WriteLine(
                $"  dont SANS décodage     {t.UnscorableDirectionCount}   ← ni indice valide ni " +
                "décodage exploitable : ce n'est pas un mode d'échec sémantique");
        Console.WriteLine();
        Console.WriteLine($"  ordre de priorité      {string.Join(" → ", FailureTaxonomy.PriorityOrder)} → {FailureModes.Unclassified}");
        Console.WriteLine();
        Console.WriteLine("  mode  part     n     libellé");

        foreach (var m in t.Distribution)
        {
            var rule = m.Mode == FailureModes.M0
                ? string.Empty
                : m.ActionJustified
                    ? "   ≥ 5 % → intervention justifiée"
                    : "   < 5 % → aucune ligne de prompt";
            Console.WriteLine($"  {m.Mode,-4}  {N(m.Share)}  {m.Count,4}  {m.Label}{rule}");
        }

        // M6 se compte en BOARDS, jamais mélangé à la distribution par direction : ce n'est pas
        // la même unité.
        Console.WriteLine();
        Console.WriteLine($"  {FailureModes.M6}    {N(t.M6Share)}  {t.M6BoardCount,4}  " +
                          $"{FailureModes.Label(FailureModes.M6)} — sur {t.BoardCount} board(s), " +
                          "unité DISTINCTE de la distribution ci-dessus");
        if (t.M6Boards.Count > 0)
            Console.WriteLine($"        boards : {string.Join(", ", t.M6Boards)}");

        Console.WriteLine();
        Console.WriteLine(
            $"  {FailureModes.M5} n'est PAS extrapolé : le harnais n'embarque aucune ressource de " +
            "fréquence lexicale. Il se pose à la main sur l'échantillon lu (--sample).");
        Console.WriteLine();
    }
}
```

- [ ] **Step 6 : Câbler le verbe et le `.gitignore`**

Dans `Program.cs`, `switch` :

```csharp
                "analyze" => AnalyzeCommand.ExecuteAsync(cliArgs, CancellationToken.None),
```

`using SoClover.Eval.Analysis;` en tête. Dans `Usage()`, après la ligne `calibrate` :

```
              analyze       P7 : taxonomie chiffree des modes d'echec (aucun appel LLM)
```

et un bloc :

```
            Taxonomie (analyze) :
              --run <run.jsonl>                 run DÉCODÉ ; refuse sinon
              --sample 20 --seed S              échantillon seedé d'échecs, lisible à la main
              --review <run.sample.md>          matrice de confusion auto ↔ humain
```

Dans `.gitignore`, sous la ligne `eval/runs/` :

```gitignore
# Taxonomie derivee : recalculable a tout moment depuis le run et son decodage.
# En revanche eval/analysis/*.sample.md est COMMITTE — il porte l'etiquetage humain.
eval/analysis/*.taxonomy.json
```

- [ ] **Step 7 : Vérifier le câblage**

Run : `dotnet run --project SoClover.Eval -- analyze`
Attendu : `ERREUR : Argument requis manquant : --run`.

Run : `dotnet run --project SoClover.Eval`
Attendu : l'usage liste `analyze`.

- [ ] **Step 8 : Suite complète et build**

Run : `dotnet build` puis `dotnet test`

- [ ] **Step 9 : Commit**

```bash
git add SoClover.Eval/Analysis/AnalysisSample.cs SoClover.Eval/Analysis/AnalyzeCommand.cs SoClover.Eval/Program.cs SoClover.Tests/Eval/AnalysisSampleTests.cs .gitignore
git commit -m "feat(eval): verbe analyze — echantillon relu a la main et matrice de confusion"
```

---

## Task 10 : Documentation et clôture

**Files:**
- Modify: `SoClover.Eval/README.md`
- Modify: `CLAUDE.md` (section « Harnais d'évaluation »)
- Modify: `Specs/AI_Clue_Eval_Loop/03_Design_Calibration_P6_P7.md` (statut + D9)

**Interfaces:**
- Consumes: tout ce qui précède
- Produces: rien de code

- [ ] **Step 1 : `SoClover.Eval/README.md` — section calibration**

Ajouter après la section « Comparer deux runs » :

````markdown
## Calibrer le décodeur (P6)

**Rien n'est publiable avant.** Toutes les lignes de `eval/LEDGER.md` portent le statut
`pré-calibration` : seule la porte du plancher aléatoire est franchie.

```bash
dotnet run --project SoClover.Eval -- calibrate \
  --comparisons eval/human/comparisons.dev.jsonl \
  --decodes 5 \
  --saturation-metrics eval/runs/<pseudo-run humain>.metrics.json \
  --floor-metrics      eval/runs/<plancher>.metrics.json \
  --notes "qwen3-8b thinking OFF, ctx 8k"
```

Le verbe re-décode les indices des deux options de chaque couple avec le décodeur **courant**,
puis rend un verdict unique sur **quatre** portes :

| Porte | Seuil |
|---|---|
| accord brut décodeur/humain, hors égalités | ≥ 0,75 |
| Cohen's κ | ≥ 0,40 |
| non-saturation (`recovery` sur indices humains `solide`) | ≤ 0,95 |
| plancher (`recovery` sur indices aléatoires) | ≤ 0,15 |

Deux artefacts, **committés** : `eval/human/calibration.<date>-<empreinte>.jsonl` (les décodages)
et `.json` (le rapport).

### L'empreinte de décodeur

`<empreinte>` = 12 hex du SHA-256 de `(modèle, prompt et sa version, température, topP,
maxOutputTokens)`. **`decodesPerClue` en est exclu** : il change la granularité de R̄, pas le
décodeur — d'où une calibration à 5 décodages et des runs à 3, sans divergence.

> **Deux `recovery` d'empreintes différentes ne se comparent pas**, au même titre que deux runs de
> `benchHash` différents. Les `.decoded.jsonl` committés portent `cluePromptVersion: 1` alors que
> `decode-clue.md` est en v2 : **le décodeur qui a produit `recovery = 0,363` n'existe plus**. P6
> commence donc par `decode --force` des deux runs dev.

### Publier une ligne `calibré`

```bash
dotnet run --project SoClover.Eval -- score \
  --run eval/runs/<runId>.jsonl \
  --calibration eval/human/calibration.<date>-<empreinte>.json \
  --ledger eval/LEDGER.md
```

Le statut passe à `calibré (<empreinte>)` **si et seulement si** les quatre portes sont franchies
**et** que le run a été décodé par ce décodeur. Sinon : **refus bruyant**, jamais de repli
silencieux. Sans le drapeau, le comportement est inchangé.

### Si une porte tombe

Retour en P3, **une variable à la fois** : prompt `decode-clue.md` (version incrémentée) → modèle
→ température / `maxOutputTokens` → `decodesPerClue`. Chaque tentative laisse son fichier daté et
empreinté ; ils s'accumulent, ils ne s'écrasent pas.

> **Interdit** : ajuster le décodeur en regardant les désaccords couple par couple. Le corpus de
> calibration est le seul juge dont on dispose ; l'optimiser contre lui fabrique un décodeur qui
> s'accorde avec 100 comparaisons et avec rien d'autre. On lit au plus une dizaine de désaccords
> pour **diagnostiquer**, jamais pour ajuster item par item.

### Le paradoxe de κ

Sur marginales déséquilibrées — et elles le seront : sur `humanVsModel`, l'humain gagnera
probablement la grande majorité des couples — `p_e` s'approche de `p_o` et **κ s'effondre malgré
un accord élevé**. Ce n'est pas un défaut du décodeur. Le rapport publie la table de contingence
complète, les marginales, κ **par famille** (`modelVsModel` est la plus informative) et PABAK
**en diagnostic** — la porte reste κ.

## Taxonomie des modes d'échec (P7)

```bash
dotnet run --project SoClover.Eval -- analyze --run eval/runs/<runId>.jsonl --sample 20 --seed 20260806
# lire eval/analysis/<runId>.sample.md, remplir « étiquette humaine : » pour les 20 items
dotnet run --project SoClover.Eval -- analyze --review eval/analysis/<runId>.sample.md
```

Chaque direction reçoit **une** étiquette, dans l'ordre `M2 → M3 → M4 → M1 → M?`.
`M6` se compte **en boards**, jamais mélangé à la distribution par direction. `M?` compte et
s'affiche : une taxonomie qui classe 100 % des items est une taxonomie qui triche. La **règle des
5 %** est imprimée mode par mode.

`M5` (jargon / mot rare) n'est **jamais** produit automatiquement : le harnais n'embarque aucune
ressource de fréquence lexicale, et un proxy inventé donnerait une fausse impression de rigueur.
Il se pose à la main sur l'échantillon lu.

`<runId>.sample.md` est **committé** (il porte l'étiquetage humain) ; `<runId>.taxonomy.json` est
dérivé et gitignoré.
````

- [ ] **Step 2 : `CLAUDE.md` — mettre à jour la section « Harnais d'évaluation des indices IA »**

Remplacer la puce **Verbes** par :

```markdown
- **Verbes** : `doctor | bench | generate | decode | score | compare | elicit | judge | human-run |
  human-report | calibrate | analyze`. Générer et décoder sont **deux passes distinctes** séparées
  par un rechargement manuel de modèle dans LM Studio (un seul modèle servi à la fois). Les deux
  sont reprenables ; `--force` repart de zéro.
```

Ajouter, après la puce « `--subset` sur `score` / `compare` » :

```markdown
- **Empreinte de décodeur (P6)** : `DecoderFingerprint` = 12 hex de `(modèle, prompt et sa version,
  température, topP, maxOutputTokens)` — **`decodesPerClue` exclu** (granularité de R̄, pas
  décodeur). Deux `recovery` d'empreintes différentes **ne se comparent pas**. `calibrate` refuse
  des `.metrics.json` produits par un autre décodeur ; `score --calibration` refuse **bruyamment**
  de publier une ligne `calibré` si une porte est tombée ou si l'empreinte diverge — jamais de
  repli silencieux en `pré-calibration`. Les 18 colonnes du registre sont préservées : l'empreinte
  vit dans la cellule *statut*.
- **Les quatre portes, en un verdict** : accord ≥ 0,75, κ ≥ 0,40, non-saturation ≤ 0,95, plancher
  ≤ 0,15. `calibrate` calcule les deux premières et **lit** les deux autres dans les `.metrics.json`
  désignés. Sans cette agrégation, on franchit « une porte sur trois » portes sur quatre.
- **Taxonomie (P7)** : ordre de priorité **`M2 → M3 → M4 → M1 → M?`** — et non l'ordre du design,
  sous lequel `M4` est structurellement inatteignable (`R̄ = 0` ⟹ ≥ 4 mots faux distincts ⟹ `M1`).
  `M6` se compte en **boards**. `M5` n'est **jamais** automatique. `analyze --review` valide
  l'étiquetage contre 20 items lus à la main (seuil indicatif 0,70).
- **Un seul bootstrap** : `Scoring/Bootstrap.Ci` sert le Δ`recovery`, l'accord et κ. Ne jamais en
  écrire un second — deux IC différents pour la même raison, et personne ne sait lequel croire.
```

- [ ] **Step 3 : Clore le design**

Dans `Specs/AI_Clue_Eval_Loop/03_Design_Calibration_P6_P7.md` :

1. Bandeau de statut :

```markdown
> **Statut** : **implémenté** (plan
> [`2026-08-03-decoder-calibration-taxonomy-p6-p7.md`](../../docs/superpowers/plans/2026-08-03-decoder-calibration-taxonomy-p6-p7.md)).
> Les chiffres restent à produire : voir §9, ordre opérationnel.
```

2. Dans le tableau §5.3, remplacer la phrase d'ordre de priorité par :

```markdown
**Ordre de priorité déterministe et documenté** : `M2 → M3 → M4 → M1 → M?`. *(Corrigé à
l'implémentation : sous l'ordre initialement écrit `M2 → M3 → M1 → M4`, `M4` est structurellement
inatteignable — `R̄ = 0` impose deux mots faux par décodage, et une intersection vide sur ≥ 2
décodages impose ≥ 4 mots faux distincts, donc `M1` à tous les coups. `R̄ = 0` est la condition
strictement plus forte : elle passe devant.)*
```

- [ ] **Step 4 : Vérifications finales**

Run : `dotnet build` puis `dotnet test`
Attendu : build propre, **suite entière verte**, aucune assertion de P0-P5 modifiée.

Run : `git diff --stat main -- SoClover/`
Attendu : **vide** — aucune modification du code de production.

Run : `grep -n "SoClover.Eval" SoClover/Dockerfile`
Attendu : **aucun résultat**.

- [ ] **Step 5 : Commit**

```bash
git add SoClover.Eval/README.md CLAUDE.md Specs/AI_Clue_Eval_Loop/03_Design_Calibration_P6_P7.md
git commit -m "docs(eval): calibration, portes et taxonomie — README, CLAUDE.md et cloture du design"
```

---

## Exécution opérationnelle (hors code — après T10)

Le code ne produit **rien** tant que les séances humaines n'ont pas eu lieu. `eval/human/` n'existe pas à ce jour. Ordre imposé par le protocole, **non négociable** :

- [ ] **1. Re-décoder les runs dev avec le décodeur courant**

```bash
dotnet run --project SoClover.Eval -- decode --run eval/runs/20260728-v5-google-gemma-4-12b-qat-d79a63b9.jsonl --force
dotnet run --project SoClover.Eval -- score  --run eval/runs/20260728-v5-google-gemma-4-12b-qat-d79a63b9.jsonl
dotnet run --project SoClover.Eval -- decode --run eval/runs/<plancher>.jsonl --force
dotnet run --project SoClover.Eval -- score  --run eval/runs/<plancher>.jsonl
```

Vérifier que la porte du plancher (`recovery ≤ 0,15`) est **toujours** franchie avec `decode-clue` v2. Elle l'était avec v1 ; **ce n'est pas une propriété acquise**.

- [ ] **2. Tenir les séances A et B** — §10 de `02_Design_Human_P4_P5.md`. Elles supposent elles-mêmes un **second run générateur** pour la famille `modelVsModel`.

- [ ] **3. `human-report` sur les deux artefacts.** Position 1 et ancres : si le lot est suspect, le consigner **au registre** *avant* d'en tirer la moindre porte.

- [ ] **4. `human-run` → `decode` → `score --subset … --subset-outcome solide`** : c'est ce `.metrics.json` qui alimente `--saturation-metrics`.

- [ ] **5. `calibrate`.** Lire le verdict. S'il est *DÉCODEUR RENVOYÉ EN P3* : une variable à la fois, et **on ne publie rien**.

- [ ] **6. `score --run <v5> --calibration … --ledger`** → **première ligne `calibré` du registre**. La ligne `pré-calibration` reste : elle n'est jamais réécrite, et l'écart entre les deux mesure l'effet du changement de décodeur, rien d'autre — mêmes indices des deux côtés.

- [ ] **7. Plafond humain, en DEUX chiffres :**

```bash
# plafond joué — A-1 : un pass reste au dénominateur
score --run <pseudo-run humain> --subset eval/human/elicitation.dev.jsonl --calibration … --ledger eval/LEDGER.md
# plafond sur paires résolues — A-3
score --run <pseudo-run humain> --subset eval/human/elicitation.dev.jsonl --subset-outcome solide,tiede --calibration … --ledger eval/LEDGER.md
# écart apparié modèle ↔ humain, avec son IC
compare --baseline <run v5> --variant <pseudo-run humain> --subset eval/human/elicitation.dev.jsonl
```

- [ ] **8. `analyze --sample 20`** → lire les 20 échecs → `analyze --review` → **taxonomie chiffrée validée**.

- [ ] **9. Trancher** (§5.6 du design) :

| Issue | Signature | Conduite |
|---|---|---|
| Marge réelle | `recovery` v5 nettement sous le plafond humain, un mode ≥ 5 % domine | Intervenir par le **rang 1** : best-of-N reranké par le décodeur. Le prompt v5 produit déjà ses `candidates`, exposés en P0 |
| Plafond atteint | `recovery` v5 à ≥ 90 % du plafond humain | **Arrêter d'investir** — résultat acceptable et instructif, *avant* les 30 h d'annotation qu'on n'aura pas dépensées |
| Instrument à bout | Portes franchies de justesse, ou plafond humain lui-même bas | Élargir le **corpus humain**, pas toucher au prompt |

- [ ] **10. Consigner au registre que `eval/boards.test.jsonl` n'a pas été ouvert**, avec la date. Le test set sert à vérifier qu'un *gain* généralise ; en P7 aucune variante n'a été promue, il n'y a rien à généraliser.

---

## Critères de fin de cycle

1. `dotnet test` passe, y compris **toutes** les suites de P0-P5, **sans modification de leurs assertions**.
2. Les quatre portes sont **évaluées et consignées**, franchies ou non. Un échec consigné est un résultat du cycle, pas une tâche inachevée.
3. `eval/LEDGER.md` contient au moins une ligne de statut `calibré`, portant son empreinte — ou, si les portes sont tombées, une ligne consignant l'échec et le renvoi en P3.
4. Le plafond humain est publié **en deux chiffres** (joué / paires résolues), avec l'écart apparié modèle ↔ humain et son IC.
5. La taxonomie est chiffrée, son étiquetage automatique est **validé contre 20 items lus à la main**, et la règle des 5 % est appliquée à la conclusion.
6. `eval/boards.test.jsonl` **n'a pas été ouvert** ; la décision est datée au registre.
7. Aucun banc committé n'a bougé (`CommittedBenchIntegrityTests` reste vert).
8. Aucun octet de code d'évaluation dans l'image Docker — `SoClover/Dockerfile` ne référence toujours que `SoClover/SoClover.csproj`.

---

## Ce que ce plan ne livre pas

- **Les interventions elles-mêmes** (rangs 1 à 6 du PRD : best-of-N, few-shot ciblé, modèle et échantillonnage, contexte inter-directions, RAG, SFT). Le chantier livre de quoi les **arbitrer**, jamais elles — c'est le non-objectif fondateur.
- Le **pack few-shot** `fewshot/pack.fr.json`, qui dérive de la séance A et relève de l'intervention de rang 2.
- La **première consultation du test set**, reportée au premier jalon d'intervention, **appariée** à la variante.
- La **langue EN** : le dispositif est transposable, mais décodeur et plafond humain sont à recalibrer intégralement.
- La **télémétrie de production** (`ValidateGuessingBoard`), sans intérêt tant que les joueurs IA ne sont pas en prod — et prioritaire le jour où ils le seront.

---

## Auto-revue du plan

**1. Couverture de la spec** — chaque section du design a sa tâche :

| Section du design | Tâche |
|---|---|
| §3 « une extraction, une seule » (`Bootstrap`) | T2 |
| §4.1 empreinte de décodeur | T3 |
| §4.2 verbe `calibrate`, lot, décodage reprenable | T3 (persistance), T4 (lot), T6 (verbe) |
| §4.3 verdict décodeur, dénominateur, `ε`, garde-fous | T5 |
| §4.4 κ, paradoxe, contingence, PABAK, IC, plafond intra-juge | T5 (calcul), T6 (impression + `IntraJudgeBelowGate`) |
| §4.5 les quatre portes en un verdict | T6 |
| §4.6 conduite si une porte tombe | T6 (impression prescriptive), README T10 |
| §4.7 formats JSONL et rapport | T3 (schémas), T6 (`CalibrationReport`) |
| §4.8 statut `calibré`, 18 colonnes, bandeau | T7 |
| §5.1 aucun re-run générateur | Exécution opérationnelle, étape 1 |
| §5.2 plafond humain en deux chiffres | Exécution opérationnelle, étape 7 |
| §5.3 taxonomie `M0`-`M6`, ordre, règle des 5 % | T8 |
| §5.4 échantillon relu, matrice de confusion | T9 |
| §5.5 test set non consulté | Exécution opérationnelle, étape 10 ; critère 6 |
| §5.6 décision de fin de chantier | Exécution opérationnelle, étape 9 |
| §6 gestion des erreurs | T3 (intégrité), T4 (couples), T6 (bench, empreinte, corpus, `ε`, reprise), T7 (refus bruyant), T9 (run non décodé, échantillon non rempli) |
| §7 stratégie de test | Une suite par tâche, `FakeChatClient`, aucun appel LLM réel |
| §8 découpage en tâches | T1-T10, à l'identique |

Le tableau §6 « Gestion des erreurs » est couvert ligne à ligne, **sauf** « lot déclaré suspect par `human-report` » qui n'a pas de test dédié : il est recopié depuis `HumanReport.ForComparisons` dans `CalibrationReport` (T6, champs `Position1Suspect` / `AnchorSuspect`) et imprimé, la logique elle-même étant déjà couverte par `HumanReportTests` de P5.

**2. Placeholders** — aucun « TBD », aucun « implémenter plus tard », aucun « comme la tâche N ». Chaque étape de code porte son bloc complet. Deux endroits restent délibérément descriptifs et sont signalés comme tels : la fabrique `AnalysisFixture` de T9 (composée de `HumanTestData.Bench` / `.Run` déjà existants, décrits champ par champ) et les insertions dans `Program.cs` (le fichier existant est cité par sa ligne d'ancrage).

**3. Cohérence des types** — vérifiée de bout en bout :

- `Bootstrap.Ci<T>` (T2) est appelé par `AgreementMetrics.Compute` (T5) avec `IReadOnlyList<VerdictPair>` et par `PairedComparison` avec `IReadOnlyList<double>` — la signature générique couvre les deux.
- `DecoderFingerprint.FromManifest(DecodeManifest)` (T3) est appelé par `CalibrationGates.FingerprintOfMetrics` (T6) et par `ScoreCommand.ResolveStatus` (T7). Même signature partout.
- `CalibrationClue` est la clé de `RBarByClue` (T4) et le paramètre de lecture dans `AgreementMetrics.Compute` (T5) — record, donc égalité structurelle : la clé fonctionne.
- `CalibrationDecode.From(ClueDecodeLine, string)` (T3) est le seul point d'entrée depuis `ClueDecoder.DecodeAsync` (T6).
- `AgreementReport` (T5) est consommé par `CalibrationGates.Evaluate` (T6) et embarqué dans `CalibrationReport` (T6), lui-même relu par `ScoreCommand` (T7) via `EvalJson.Deserialize`.
- `TaxonomyReport` / `DirectionLabel` (T8) sont consommés par `AnalysisSample.Draw` (T9).
- `FailureModes.All` / `IsKnown` (T8) sont utilisés par le rendu et le parsing de T9.
- `LedgerWriter.CalibratedStatusFor` (T7) est la seule fabrique du libellé de statut.

**4. Le point le plus fragile du plan**, à surveiller en revue : `CalibrationSet.Build` (T4) replie les doublons inversés en s'appuyant sur `ReferenceEquals` avec le résultat de `HumanFile.LatestByComparisonId`. Cela fonctionne parce que `LatestByComparisonId` rend l'instance issue de la liste (`g.Last()`), pas une copie. Si un jour ce contrat change, le repli devient silencieusement faux — le test `A_rejudged_comparison_keeps_only_its_last_verdict` est le garde-fou, et il doit rester.
