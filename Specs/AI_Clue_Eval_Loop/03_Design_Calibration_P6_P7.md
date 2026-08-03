# Design — Calibration du décodeur et baseline officielle (P6 → P7)

> **Statut** : design validé, plan d'implémentation écrit
> ([`2026-08-03-decoder-calibration-taxonomy-p6-p7.md`](../../docs/superpowers/plans/2026-08-03-decoder-calibration-taxonomy-p6-p7.md)).
> **Aucun code écrit.**
> **Périmètre** : phases **P6** (portes de calibration accord / κ) et **P7** (baseline officielle,
> plafond humain publié, taxonomie chiffrée des modes d'échec) du PRD
> [`00_Overview.md`](00_Overview.md). Dernier cycle du chantier.
> **Suite de** : [`01_Design_Harness_P0_P3.md`](01_Design_Harness_P0_P3.md) et
> [`02_Design_Human_P4_P5.md`](02_Design_Human_P4_P5.md).
>
> **Prérequis opérationnel non satisfait à ce jour** : `eval/human/` n'existe pas. L'outillage
> P4-P5 est livré mais **les deux séances humaines n'ont pas été tenues**. Tout l'outillage décrit
> ici se développe et se teste sur corpus fabriqués — mais **aucun chiffre ne sort avant les
> séances**, et P6 est précisément la phase qui refuse de produire un chiffre sans elles.

---

## 1. Ce que ce cycle vient réparer

Les deux cycles précédents ont livré un instrument qui tourne et un protocole humain qui
s'auto-applique. Il manque exactement trois choses, et ce sont celles qui rendent les chiffres
**publiables** plutôt que seulement calculables.

| Manque | État aujourd'hui | Ce que ce cycle apporte |
|---|---|---|
| Le décodeur n'est pas légitimé | Toutes les lignes de `eval/LEDGER.md` portent le statut `pré-calibration`. Seule la porte du plancher aléatoire est franchie | **P6** : accord décodeur/humain et κ, calculés sur la séance B, et un statut `calibré` que le registre ne peut pas s'attribuer tout seul |
| `recovery` n'a pas de haut d'échelle | `0,363` pour v5, contre un plancher de `0,130`. Rien ne dit s'il reste de la marge | **P7** : plafond humain publié, en deux chiffres (joué / sur paires résolues) |
| Les modes d'échec sont anecdotiques | On sait que `half_rate = 0,656` domine ; on ne sait pas comment ça se répartit ni où intervenir | **P7** : taxonomie chiffrée, étiquette par direction, règle des 5 % appliquée |

> **P6 est une porte, pas une étape.** Tant qu'elle n'est pas franchie, aucun chiffre du décodeur
> n'est publiable au registre. C'est la seule phase du chantier dont l'issue légitime est *« l'outil
> est cassé, retour en P3 »* — et la seule dont il serait tentant de se dispenser, précisément parce
> que l'instrument produit déjà des nombres d'allure convaincante.

---

## 2. Décisions arrêtées

| Sujet | Décision | Motif |
|---|---|---|
| Source des R̄ de calibration | Verbe **`calibrate` autonome**, qui re-décode les indices de `comparisons.dev.jsonl` | Les `assistedClue` n'appartiennent à **aucun run** (`HumanRunExport.Build` ne projette que les lignes `elicitation`) ; joindre sur les `.decoded.jsonl` exigerait un pseudo-run assisté *et* la vérification que quatre runs ont été décodés par le même décodeur. Le verbe autonome garantit en plus que les **deux options d'un couple sont décodées à l'identique** |
| Granularité de décodage en calibration | `--decodes 5` recommandé (défaut du verbe `decode` inchangé à 3) | Avec `half_rate = 0,656`, une masse de couples serait ex æquo à `R̄ = 0,5` des deux côtés. Passer de 3 à 5 décodages fait passer la granularité de 1/6 à 1/10 et desserre le dénominateur de l'accord. Coût : ~180 indices distincts × 5 ≈ 900 décodages, ~6 min en local (référence P0-P3 : 480 décodages en 3 min 11) |
| Famille `anchor` | **Exclue du calcul principal**, rapportée à part | Le design P4-P5 pose déjà que l'écart de qualité évident du plancher gonflerait artificiellement l'accord et κ. Les ancres restent un contrôle de bon sens du décodeur, pas une mesure |
| Test set en P7 | **Non consulté.** Décision datée, consignée | Le test set sert à vérifier qu'un *gain* généralise. En P7 aucune variante n'a été promue : il n'y a rien à généraliser. L'ancrage test se mesurera apparié à la première variante promue, sur les mêmes items — rien n'est perdu, une consultation est économisée |
| Re-run générateur en P7 | **Aucun** | Le run v5 dev existe et le générateur n'a pas bougé. Seul le décodage est refait : les deux lignes du registre portent alors sur **exactement les mêmes indices**, ce qui isole proprement l'effet du changement de décodeur |
| Langue | FR uniquement | Conforme au cadrage du PRD |

---

## 3. Architecture d'ensemble

```
SoClover.Eval/
  Calibration/
    CalibrationModel.cs     schémas JSONL : manifeste, décodage de calibration, rapport
    DecoderFingerprint.cs   empreinte du décodeur — ce qui rend deux recovery comparables
    CalibrationSet.cs       comparisons.dev.jsonl -> indices distincts à décoder, couples retenus
    AgreementMetrics.cs     verdict décodeur, accord, κ, contingence, IC bootstrap
    CalibrationGates.cs     les QUATRE portes réunies, verdict unique
    CalibrateCommand.cs     verbe calibrate
  Analysis/
    FailureTaxonomy.cs      signatures M0-M6, étiquette unique, ordre de priorité
    AnalysisSample.cs       échantillon seedé lisible + relecture de l'étiquetage humain
    AnalyzeCommand.cs       verbe analyze
  Io/
    CalibrationFile.cs      JSONL append-only reprenable (même contrat que RunFile / DecodeFile)
  Scoring/Bootstrap.cs      IC bootstrap partagé, extrait de PairedComparison
  Program.cs                + verbes calibrate | analyze

eval/
  human/calibration.<date>-<fingerprint>.jsonl   COMMITTÉ — décodages de calibration
  human/calibration.<date>-<fingerprint>.json    COMMITTÉ — rapport, portes, verdict
  analysis/<runId>.taxonomy.json                 dérivé
  analysis/<runId>.sample.md                     COMMITTÉ — les 20 échecs lus à la main
```

**Aucune modification du code de production.** Le `Dockerfile` ne référence toujours que
`SoClover/SoClover.csproj` et `docs/deploy.md` fait toujours `git archive HEAD … SoClover/` :
**zéro octet d'éval en prod**.

**Réutilisations, à ne pas réécrire** : `ClueDecoder` (tel quel — il décode un indice quelconque,
il n'a jamais rien su du run dont l'indice provient), `ShuffleSeed`, `Xoshiro256SS`, `EvalJson`,
`BenchFile.Read`, `HumanFile.ReadComparisons`, `SubsetSelector`, `RunMetrics.Compute`, `DecodeFile`,
`LedgerWriter`.

**Une extraction, une seule** : `PairedComparison.BootstrapCi` devient `Scoring/Bootstrap.Ci`,
paramétré par une statistique (moyenne pour le Δ`recovery`, proportion pour l'accord, κ pour κ).
Refactor sans changement de comportement, avec `PairedComparisonTests` en filet. Dupliquer un
bootstrap est le genre de dette qui produit ensuite deux IC légèrement différents pour la même
raison, sans que personne ne sache lequel croire.

---

## 4. P6 — calibration du décodeur

### 4.1 Le constat déclencheur : l'empreinte de décodeur

Les `.decoded.jsonl` committés portent `cluePromptVersion: 1`. Le fichier
`SoClover.Eval/Decoder/Prompts/fr/decode-clue.md` est aujourd'hui en **v2** (commit `a72921b`,
« cadrage du niveau de raisonnement attendu »). **Le décodeur qui a produit `recovery = 0,363`
n'existe plus.**

P6 commence donc obligatoirement par `decode --force` des deux runs dev — baseline v5 et plancher
aléatoire — avec le décodeur courant. Sans ça, la calibration validerait un décodeur dont *aucun
chiffre du registre n'est issu* : une porte franchie sur un instrument, des chiffres publiés par un
autre.

Ce cas ne doit pas pouvoir se reproduire silencieusement. D'où :

```csharp
public static class DecoderFingerprint
{
    // 12 premiers caractères hex du SHA-256 de la config canonicalisée.
    public static string Compute(
        string modelId, string cluePromptFile, int? cluePromptVersion,
        double temperature, double? topP, int? maxOutputTokens);

    public static string FromManifest(DecodeManifest manifest);
}
```

**`decodesPerClue` en est délibérément exclu** : il change la *granularité* de R̄, pas le décodeur.
C'est ce qui autorise une calibration à 5 décodages et des runs à 3 sans que les empreintes
divergent. Le nombre de décodages reste consigné à part, dans les deux manifestes.

> **Ce que l'empreinte rend impossible** : publier une ligne `calibré` avec un décodeur autre que
> celui qui a franchi les portes. Deux `recovery` d'empreintes différentes ne se comparent pas — au
> même titre que deux runs de `benchHash` différents, que `compare` refuse déjà.

### 4.2 Le verbe `calibrate`

```bash
dotnet run --project SoClover.Eval -- calibrate \
  --comparisons eval/human/comparisons.dev.jsonl \
  --bench eval/boards.dev.jsonl \
  --decodes 5 \
  --saturation-metrics eval/runs/human-<…>.metrics.json \
  --floor-metrics      eval/runs/<plancher>.metrics.json
```

1. **Constituer le lot** (`CalibrationSet`) : les indices distincts par `(boardId, direction, clue)`
   sur les deux options de chaque couple retenu, toutes familles confondues. Un doublon inversé
   (`duplicateOf`) désigne le **même couple** : il ne le compte qu'une fois, en retenant le dernier
   verdict — « la dernière ligne gagne », la règle de lecture déjà en place dans `HumanFile`.
2. **Décoder** via `ClueDecoder`, `--decodes` fois par indice. L'ordre de présentation vient de
   `ShuffleSeed.ForClue(benchHash, boardId, decodeIndex)`, qui ne dépend **ni de la direction ni de
   l'indice** : les deux options d'un couple voient donc le même ordre à `decodeIndex` égal.
   L'ordre de présentation ne peut structurellement pas expliquer une préférence du décodeur — le
   contrôle est apparié, gratuitement.
3. **Agréger** (`AgreementMetrics`), **évaluer les quatre portes** (`CalibrationGates`), imprimer et
   écrire le rapport.

Le décodage est **reprenable** : clé `(boardId, direction, clue, decodeIndex)`, `--force` repart de
zéro. Même religion que `RunFile` et `DecodeFile`.

> **Un type de ligne dédié, et pourquoi.** `ClueDecodeLine` a pour clé
> `(boardId, direction, decodeIndex)` — elle suffit à un run, où une direction porte un seul indice.
> En calibration, une direction porte **deux à trois** indices concurrents (humain, modèle, assisté).
> Réutiliser `ClueDecodeLine` telle quelle les ferait collisionner silencieusement à la reprise.
> `CalibrationDecode` est `ClueDecodeLine` **plus le champ `clue`**.

### 4.3 Le verdict du décodeur, et le piège du dénominateur

Pour chaque couple, `R̄_A` et `R̄_B` sont calculés sur les décodages **valides** ; un décodage en
échec de format reste **exclu du dénominateur**, jamais compté 0 — invariant déjà tenu par
`RunMetrics`, et pour la même raison : un décodeur qui ne sait pas répondre au format n'est pas un
décodeur qui se trompe.

| Condition | Verdict décodeur |
|---|---|
| `R̄_A − R̄_B > ε` | `A` |
| `R̄_B − R̄_A > ε` | `B` |
| sinon | `tie` |

`ε = 0` par défaut — tout écart compte —, `--epsilon` pour explorer. **`ε` n'est pas un réglage
libre** : le modifier après avoir vu l'accord serait ajuster l'instrument sur sa propre mesure.
Toute valeur non nulle doit être décidée *avant* de lire le résultat et consignée dans le manifeste.

**L'accord brut est calculé hors égalités**, comme le prescrit le PRD :

```
accord = #(verdicts identiques) / #(couples où humain ET décodeur tranchent)
```

C'est ici qu'est le risque, et il est sérieux : avec `half_rate = 0,656`, une large fraction des
couples aura `R̄_A = R̄_B = 0,5` — deux indices qui attrapent chacun une seule face. Le décodeur dit
`tie`, le couple sort du dénominateur, et la porte finit par se jouer sur trente comparaisons.

Trois garde-fous, tous obligatoires :

- `humanTieRate` et `decoderTieRate` **publiés à côté de l'accord**, jamais en note de bas de page ;
- **avertissement explicite sous 40 couples** dans le dénominateur — un accord de 0,80 sur 28
  couples a un IC qui traverse la porte, et le rapport doit le dire ;
- `--decodes 5` recommandé, précisément pour desserrer ce dénominateur.

**Périmètre des familles** : `humanVsModel`, `modelVsModel` et `humanVsAssisted` entrent dans le
calcul principal. `anchor` en est **exclue** et rapportée à part (attendu : le décodeur préfère
l'indice réel à l'aléatoire sur ≥ 4 des 5 ancres). Y inclure les ancres reviendrait à mesurer
l'accord sur des couples dont l'écart de qualité est évident — exactement ce que le design P4-P5
refusait déjà de faire côté humain.

### 4.4 Cohen's κ, et son paradoxe

κ se calcule sur les mêmes couples doublement tranchés, à **deux catégories `A` / `B`, sur les slots
canoniques** — jamais sur les positions. C'est l'invariant central du design P4-P5 : `optionA` et
`optionB` sont ordonnés par source de façon déterministe, `presentedOrder` dit seulement lequel fut
affiché en position 1. Un κ calculé sur les positions mesurerait le biais de position, pas l'accord.

```
p_o = accord observé
p_e = Σ_c  P_humain(c) · P_décodeur(c)          (c ∈ {A, B})
κ   = (p_o − p_e) / (1 − p_e)
```

> **Le paradoxe de κ, à documenter avant de le rencontrer.** Quand les marginales sont
> déséquilibrées — et elles le seront : sur `humanVsModel`, l'humain gagnera probablement la grande
> majorité des couples —, `p_e` s'approche de `p_o` et **κ s'effondre malgré un accord élevé**. Un
> accord de 0,90 avec 90 % de victoires humaines des deux côtés donne un κ voisin de zéro. Ce n'est
> pas un défaut du décodeur, c'est une propriété connue de κ sur distribution asymétrique.

Parades, dans l'ordre :

1. Publier la **table de contingence 2×2 complète** et les marginales des deux juges. C'est la seule
   façon de distinguer « le décodeur est mauvais » de « la statistique est mal conditionnée ».
2. Calculer **κ par famille** en plus du κ global. `modelVsModel` a des marginales naturellement
   plus équilibrées : c'est la famille la plus informative pour κ, et c'est pour ça que le design
   P4-P5 lui a réservé ~40 couples.
3. Rapporter `PABAK = 2·p_o − 1` **en diagnostic uniquement**. Le PRD nomme κ ; la porte reste κ.
   Publier PABAK comme porte serait changer la règle en cours de partie.
4. Si κ échoue **avec** un accord ≥ 0,75 et des marginales franchement déséquilibrées, la conduite
   prescrite est de **réviser le seuil de façon datée et justifiée au registre** — ce que le PRD
   autorise explicitement pour tous ses seuils — et non de bricoler le décodeur jusqu'à ce que le
   chiffre passe.

**IC bootstrap** sur l'accord et sur κ : rééchantillonnage des couples avec remise, `Scoring.Bootstrap`
partagé, déterministe à seed fixé. Une porte à 0,75 franchie à 0,76 avec un IC `[0,61 ; 0,88]` doit
être **visiblement** fragile, pas discrètement franchie.

**Le plafond réaliste de l'accord** : `HumanReport.ForComparisons` calcule déjà
`IntraJudgeAgreement` sur les doublons inversés. Le rapport de calibration l'affiche **à côté** de
l'accord décodeur/humain. Exiger du décodeur un accord supérieur à celui du juge avec lui-même n'a
aucun sens ; si la cohérence intra-juge est inférieure à 0,75, la porte est **inatteignable par
construction** et le rapport doit le dire — c'est alors une information sur le corpus, pas sur le
décodeur.

### 4.5 Les quatre portes, réunies en un verdict

| Porte | Seuil | Producteur |
|---|---|---|
| Accord brut décodeur/humain, hors égalités | **≥ 0,75** | `calibrate` |
| Cohen's κ | **≥ 0,40** | `calibrate` |
| Non-saturation : `recovery` sur indices humains `solide` | **≤ 0,95** | `score --subset … --subset-outcome solide` |
| Plancher : `recovery` sur indices aléatoires | **≤ 0,15** | `score` sur le run plancher — **à refranchir** après re-décodage v2 |

Ces seuils sont **repris verbatim** du tableau « Portes d'acceptation » du PRD. Un seuil qui dérive
d'une spec à l'autre est exactement ce que le registre est censé rendre impossible.

`calibrate` calcule les deux premières et **lit les deux autres** dans les `.metrics.json` désignés
par `--saturation-metrics` et `--floor-metrics`, puis rend un **verdict unique** :

```
DÉCODEUR VALIDÉ            les quatre portes franchies, empreinte <fingerprint>
DÉCODEUR RENVOYÉ EN P3     porte(s) non franchie(s) : κ = 0,31 (< 0,40)
```

Sans cette agrégation, l'opérateur recolle quatre chiffres produits par trois commandes différentes,
et franchit une porte sur trois portes sur quatre sans que rien ne le signale. Le verbe **refuse**
si les métriques citées ne portent pas la même empreinte de décodeur que la calibration.

### 4.6 Si une porte tombe

Retour en P3. Leviers ordonnés, **une variable à la fois**, chaque tentative laissant son fichier de
calibration daté et empreinté — ils s'accumulent, ils ne s'écrasent pas :

1. prompt `decode-clue.md`, version incrémentée (c'est ce qui vient d'être fait pour v2) ;
2. modèle décodeur ;
3. température / `maxOutputTokens` ;
4. `decodesPerClue`.

> **Interdit explicite** : ajuster le décodeur en regardant les désaccords couple par couple. Le
> corpus de calibration est le **seul juge dont on dispose** ; l'optimiser contre lui, c'est
> fabriquer un décodeur qui s'accorde avec 100 comparaisons et avec rien d'autre. On lit au plus une
> dizaine de désaccords pour **diagnostiquer** — le décodeur est-il trop littéral, trop savant,
> sensible à la longueur de l'indice ? — jamais pour ajuster item par item.

### 4.7 Formats

`eval/human/calibration.<date>-<fingerprint>.jsonl` — ligne 1, manifeste :

```json
{"kind":"manifest","calibrationId":"20260805-3f2a91c4e0d1","createdAtUtc":"…",
 "comparisonsFile":"eval/human/comparisons.dev.jsonl","benchFile":"eval/boards.dev.jsonl",
 "benchHash":"416b819a41a1","coupleCount":100,"clueCount":187,
 "decoderFingerprint":"3f2a91c4e0d1","provider":"OpenAI","baseUrl":"http://localhost:1234/v1",
 "modelId":"qwen/qwen3-8b","modelSnapshotDate":"2026-08-05","providerModelListHash":"…",
 "temperature":0.3,"topP":null,"maxOutputTokens":512,
 "cluePromptFile":"…/fr/decode-clue.md","cluePromptVersion":2,
 "decodesPerClue":5,"epsilon":0.0,"harnessVersion":1,"operatorNotes":"qwen3-8b thinking OFF, ctx 8k"}
```

Lignes suivantes :

```json
{"kind":"calibrationDecode","boardId":"dev-007","direction":"Top","clue":"Pédiatre",
 "decodeIndex":0,"picked":["Chirurgien","Enfant"],"r":1.0,
 "shuffleSeed":"…","decodeFailureKind":null,"latencyMs":2900}
```

`eval/human/calibration.<date>-<fingerprint>.json` — le rapport : accord et son IC, κ et son IC, κ
par famille, table de contingence, marginales, `humanTieRate`, `decoderTieRate`, dénominateur,
`intraJudgeAgreement`, PABAK, résultat des ancres, les quatre portes avec leur valeur et leur
verdict, et le verdict global. **Les deux artefacts sont committés** : ils sont, avec les deux
corpus humains, l'investissement irremplaçable du chantier.

### 4.8 Le statut `calibré` au registre

`LedgerWriter` gagne `CalibratedStatus = "calibré"` à côté de `PreCalibrationStatus`, aujourd'hui
appliqué en dur (`Scoring/ScoreCommand.cs:56`).

```bash
score --run … --calibration eval/human/calibration.<date>-<fingerprint>.json --ledger eval/LEDGER.md
```

Le statut passe à `calibré` **si et seulement si** :

- le fichier de calibration déclare **les quatre portes franchies**, et
- son `decoderFingerprint` est **celui du `.decoded.jsonl`** du run scoré.

Sinon, **refus bruyant** — jamais de dégradation silencieuse en `pré-calibration`. Qui passe le
drapeau veut publier une ligne défendable ; produire à la place une ligne mal étiquetée serait le
pire des deux mondes. Sans le drapeau, le comportement actuel est **inchangé**.

`LedgerWriter.ColumnCount = 18` est **préservé** : seul le bandeau d'en-tête gagne un paragraphe
expliquant le statut `calibré` et l'empreinte. Les lignes existantes ne sont **jamais réécrites** —
les runs antérieurs obtiennent une ligne `calibré` par re-score après re-décodage, et les deux
lignes coexistent. C'est ce qui rendra lisible, dans six mois, l'effet du passage de `decode-clue`
v1 à v2.

---

## 5. P7 — baseline officielle, plafond, taxonomie

### 5.1 Ce que P7 ne refait pas

**Aucun re-run générateur.** Le run `20260728-v5-google-gemma-4-12b-qat-d79a63b9` existe, le
générateur n'a pas bougé, et le re-générer coûterait onze minutes pour produire d'autres indices
— la température est à 1,0. Seul le décodage est refait. Bénéfice méthodologique direct : la ligne
`pré-calibration` et la ligne `calibré` portent sur **exactement les mêmes indices**, et leur écart
mesure l'effet du changement de décodeur, rien d'autre.

### 5.2 Le plafond humain publié

```
human-run  →  decode (décodeur CALIBRÉ)  →  score --subset --calibration
```

**Deux chiffres, jamais un** :

| Lecture | Commande | Ce qu'elle dit |
|---|---|---|
| Plafond **joué** | `score --subset elicitation.dev.jsonl` | Ce qu'un humain produit en conditions réelles, `pass` compris. A-1 : un `pass` reste au dénominateur |
| Plafond sur **paires résolues** | `+ --subset-outcome solide,tiede` | A-3 : « plafond mesuré sur les X % de paires résolues », et rien de plus |

Puis `compare --baseline <run v5> --variant <run humain> --subset …` pour l'écart apparié
modèle ↔ humain avec son IC bootstrap.

Le **taux de `pass`** de la séance A arrive avec, gratuitement, et c'est lui qui arbitre
l'intervention de rang 4 du PRD (contexte inter-directions) : il mesure la fraction du plateau où
même un humain expert n'a pas d'indice, donc la fraction relevant d'une stratégie au niveau du
board, structurellement hors d'atteinte de `PerDirection`.

### 5.3 Le verbe `analyze` — taxonomie chiffrée

```bash
dotnet run --project SoClover.Eval -- analyze --run eval/runs/<runId>.jsonl [--sample 20 --seed S]
```

Le PRD donne l'intention des modes ; la spec doit les rendre **calculables**. Chaque direction
reçoit **une** étiquette.

| Code | Mode | Signature opératoire |
|---|---|---|
| `M0` | réussi | `R̄ ≥ 0,75`. Pas un mode d'échec — mais il faut un dénominateur honnête |
| `M2` | n'attrape qu'une face (« Hôpital ») | ≥ 2 décodages à `R = 0,5` **et la même face manquée** à chaque fois |
| `M3` | collision avec un distracteur | un même mot non-référence choisi dans ≥ 2 décodages — signature **concentrée** |
| `M1` | trop générique | `R̄ ≤ 0,5` **et** mots faux **dispersés** : ≥ 4 mots non-référence distincts sur l'ensemble des décodages |
| `M4` | relation trop indirecte | `R̄ = 0` **et** aucun mot commun aux paires choisies aux différents décodages |
| `M5` | jargon / mot rare | **non détectable automatiquement** — voir ci-dessous |
| `M6` | collision inter-directions | signature **au niveau board** : moyenne des R̄ des 4 directions ≥ 0,5 alors que `boardPositions` du même board lui est inférieur d'au moins 0,20 |
| `M?` | non classé | ne satisfait aucune signature |

**Ordre de priorité déterministe et documenté** : `M2 → M3 → M1 → M4 → M?`. Les signatures se
recouvrent — une direction peut être à la fois dispersée et concentrée sur un mot — et une
taxonomie dont l'ordre d'évaluation n'est pas écrit produit des distributions non reproductibles.
`M6` est compté **séparément, en boards**, jamais mélangé à la distribution par direction : ce n'est
pas la même unité.

> **`M5` reste manuel, et c'est une décision, pas un oubli.** Le harnais n'embarque aucune ressource
> de fréquence lexicale, et en ajouter une (choix du corpus, licence, poids, couverture du français)
> serait hors de proportion avec l'usage. Un proxy inventé — longueur du mot, absence du
> dictionnaire du jeu — étiquetterait mal et donnerait une fausse impression de rigueur. `M5` est
> posé à la main sur l'échantillon lu, et le rapport indique explicitement qu'il n'est pas
> extrapolé.

**`M?` compte et s'affiche.** Une taxonomie qui classe 100 % des items est une taxonomie qui triche.

**La règle des 5 % est imprimée**, mode par mode : « ≥ 5 % → intervention justifiée » /
« < 5 % → aucune ligne de prompt ». C'est ce qui empêche le prompt de devenir un empilement de 240
lignes dont plus personne ne sait quelle partie sert encore.

### 5.4 Valider l'étiquetage plutôt que le croire

`--sample 20 --seed S` tire un échantillon seedé parmi les directions en échec (`R̄ < 0,75`) et écrit
`eval/analysis/<runId>.sample.md` — un fichier **lisible par un humain**, pas un JSON : pour chaque
item, l'indice, la paire cible, les 16 mots, les décodages obtenus, l'étiquette automatique
proposée, et une ligne vide pour l'étiquette humaine.

L'opérateur remplit, puis :

```bash
analyze --review eval/analysis/<runId>.sample.md
```

qui rend la **matrice de confusion auto ↔ humain** et le taux d'accord. Seuil indicatif **≥ 0,70** ;
en dessous, les seuils des signatures sont à revoir, et la révision est datée.

C'est la matérialisation du « lire ~20 échecs, les ranger en modes d'échec, compter » de la boucle
du PRD. Sans elle, on publie une distribution d'étiquettes que **personne n'a jamais vérifiée** —
et une taxonomie fausse est plus coûteuse qu'aucune taxonomie, puisqu'elle oriente les
interventions.

### 5.5 Le test set n'est pas consulté

Décision datée, à consigner dans le registre au moment de la ligne baseline P7.

Le test set sert à vérifier qu'un **gain** généralise. En P7, aucune variante n'a été promue : il
n'y a rien à généraliser, et une consultation « pour avoir un point d'ancrage » dépenserait un
budget dont le PRD fait explicitement une information sur le risque de surapprentissage. Le point
d'ancrage test se mesurera au premier jalon d'intervention, **apparié** à la variante, sur les mêmes
items — c'est même la seule façon correcte de le mesurer. Rien n'est perdu.

### 5.6 La décision de fin de chantier

P7 n'est pas un livrable technique, c'est un **jalon de décision**. Trois issues, et leur conduite :

| Issue | Signature | Conduite |
|---|---|---|
| Marge réelle | `recovery` v5 nettement sous le plafond humain, un mode d'échec ≥ 5 % domine | Intervenir par le **rang 1** du PRD : best-of-N reranké par le décodeur. Le prompt v5 produit déjà ses `candidates`, exposés en P0 — on optimise littéralement l'objectif, sans aucune donnée annotée |
| Plafond atteint | `recovery` v5 à ≥ 90 % du plafond humain | **Arrêter d'investir.** Le PRD qualifie explicitement cette issue de résultat acceptable et instructif — elle arrive *avant* les 30 h d'annotation qu'on n'aura pas dépensées |
| Instrument à bout | Portes franchies de justesse, ou plafond humain lui-même bas | Élargir le **corpus humain**, pas toucher au prompt. Un instrument sans résolution ne départagera jamais v5 de v6, et chaque expérience menée avec lui est du temps perdu |

Cette section est le point où le chantier accepte de conclure « l'instrument est trop lourd pour
l'usage » — le dernier risque listé par le PRD, et le seul que seul l'auteur du chantier peut voir.

---

## 6. Gestion des erreurs

| Situation | Comportement |
|---|---|
| `benchHash` du banc ≠ celui des comparaisons | Refus de démarrer |
| Empreinte de décodeur divergente entre calibration et `.metrics.json` cités | Refus — les quatre portes doivent porter sur **le même** décodeur |
| Corpus humain absent ou séance incomplète | Refus explicite. Une porte franchie sur un lot partiel n'est pas une porte |
| Dénominateur de l'accord < 40 couples | Avertissement ; le chiffre est publié **marqué fragile**, avec son IC |
| Cohérence intra-juge < 0,75 | Avertissement : la porte à 0,75 est inatteignable par construction, le problème est le corpus |
| Lot déclaré suspect par `human-report` (position 1, ancres) | Avertissement repris dans le rapport de calibration — le PRD demande que ce soit consigné **dans le registre** |
| `score --calibration` sur des portes non franchies | Refus bruyant, jamais de repli silencieux en `pré-calibration` |
| `--epsilon` non nul | Autorisé, consigné dans le manifeste. Le modifier après avoir lu l'accord est une faute de protocole que l'artefact rendra visible |
| Verbe relancé sur un fichier existant | Complète ce qui manque ; `--force` repart de zéro |
| `analyze` sur un run non décodé | Refus : la taxonomie se lit sur les décodages, pas sur les indices |
| `analyze --review` sur un échantillon non rempli | Refus, avec le nombre de lignes vides |

---

## 7. Stratégie de test

TDD, dans `SoClover.Tests/Eval/`. **Aucun appel LLM réel** — le `FakeChatClient` existant sert le
décodeur de calibration comme il sert déjà `ClueDecoder`.

| Cible | Ce qui est vérifié |
|---|---|
| `DecoderFingerprint` | Stable à config égale ; change sur chacun des cinq champs ; **ne change pas** quand `decodesPerClue` change |
| `CalibrationFile` | Round-trip ; reprise depuis un fichier tronqué en milieu de ligne ; refus de `harnessVersion` / `benchHash` / empreinte divergents ; deux indices distincts d'une même direction **ne collisionnent pas** |
| `CalibrationSet` | Indices dédoublonnés ; ancres écartées du lot principal et rapportées à part ; doublon inversé compté **une seule fois**, dernier verdict retenu ; couple aux deux indices identiques absent |
| `AgreementMetrics` | Verdict décodeur avec et sans `ε` ; accord = 1 sur corpus concordant ; κ ≈ 0 sur juges indépendants ; **κ inchangé quand `presentedOrder` est inversé** (l'invariant canonique, testé explicitement) ; **paradoxe de κ reproduit** — accord 0,90 et κ bas sur marginales déséquilibrées ; ties exclus du dénominateur ; avertissement sous 40 couples |
| `Bootstrap` | Déterministe à seed fixé ; IC d'une proportion connue ; **non-régression de `PairedComparison` après extraction, sans modification d'assertion** |
| `CalibrationGates` | Les quatre portes ; chaque combinaison d'échec ; verdict global ; refus sur empreintes divergentes ; portes lues depuis des `.metrics.json` fabriqués |
| `score --calibration` | Statut `calibré` **seulement** si portes franchies **et** empreinte concordante ; refus bruyant sinon ; statut inchangé sans le drapeau ; **18 colonnes préservées** |
| `FailureTaxonomy` | Chaque signature sur un item fabriqué ; ordre de priorité `M2 → M3 → M1 → M4` sur un item qui en satisfait plusieurs ; `M?` compté ; `M6` calculé en boards ; règle des 5 % appliquée à l'impression |
| `AnalysisSample` | Échantillon reproductible à seed fixé, tiré parmi les seuls échecs ; round-trip du fichier `.sample.md` ; matrice de confusion ; refus si des étiquettes humaines manquent |
| Non-régression | **Toutes les suites Eval et AI de P0-P5 passent sans modification d'assertion** |

Après chaque tâche : `dotnet test` complet et build propre, conformément à `CLAUDE.md`. Le front
n'est pas touché par ce cycle.

---

## 8. Découpage en tâches

Commits atomiques, un par tâche.

| # | Tâche |
|---|---|
| T1 | Ce document (commit documentaire seul) |
| T2 | `Scoring/Bootstrap.cs` — extraction depuis `PairedComparison`, **le refactor en premier**, tests existants en filet |
| T3 | `Calibration/CalibrationModel.cs` + `DecoderFingerprint.cs` + `Io/CalibrationFile.cs` |
| T4 | `Calibration/CalibrationSet.cs` |
| T5 | `Calibration/AgreementMetrics.cs` — accord, κ, contingence, IC |
| T6 | `Calibration/CalibrationGates.cs` + verbe `calibrate` |
| T7 | Statut `calibré` : `LedgerWriter.CalibratedStatus`, `score --calibration`, bandeau du registre |
| T8 | `Analysis/FailureTaxonomy.cs` |
| T9 | `Analysis/AnalysisSample.cs` + verbe `analyze` (`--sample`, `--review`) |
| T10 | Documentation : `SoClover.Eval/README.md`, `CLAUDE.md`, clôture de ce design |

---

## 9. Exécution opérationnelle

Le code ne produit rien tant que les séances n'ont pas eu lieu. Ordre imposé par le protocole :

1. **Re-décoder** les runs dev avec le décodeur courant : `decode --run <v5> --force` et
   `decode --run <plancher> --force`, puis `score` sur chacun. Vérifier que la porte du plancher
   (`recovery ≤ 0,15`) est **toujours** franchie avec `decode-clue` v2 — elle l'était avec v1, ce
   n'est pas une propriété acquise.
2. **Séances A et B** si elles n'ont pas encore été tenues — §10 de
   [`02_Design_Human_P4_P5.md`](02_Design_Human_P4_P5.md). Elles supposent elles-mêmes un **second
   run générateur** pour la famille `modelVsModel`.
3. `human-report` sur les deux artefacts. Position 1 et ancres : si le lot est suspect, le consigner
   au registre **avant** d'en tirer la moindre porte.
4. `human-run` → `decode` (décodeur courant) → le pseudo-run humain est décodé.
5. **`calibrate`**, avec `--saturation-metrics` (plafond `solide`) et `--floor-metrics` (plancher).
   Lire le verdict. S'il est *RENVOYÉ EN P3* : §4.6, une variable à la fois, et on ne publie rien.
6. `score --run <v5> --calibration … --ledger` → **première ligne `calibré` du registre**.
7. `score --subset` (deux lectures) et `compare --subset` → **plafond humain publié**.
8. `analyze --sample 20` → lire les 20 échecs → `analyze --review` → **taxonomie chiffrée validée**.
9. Trancher : §5.6.

---

## 10. Critères de fin de cycle

1. `dotnet test` passe, y compris toutes les suites de P0-P5, sans modification de leurs assertions.
2. Les quatre portes sont **évaluées et consignées**, franchies ou non. Un échec consigné est un
   résultat du cycle, pas une tâche inachevée.
3. `eval/LEDGER.md` contient au moins une ligne de statut `calibré`, portant son empreinte de
   décodeur — ou, si les portes sont tombées, une ligne consignant l'échec et le renvoi en P3.
4. Le plafond humain est publié **en deux chiffres** (joué / paires résolues), avec l'écart apparié
   modèle ↔ humain et son IC.
5. La taxonomie est chiffrée, son étiquetage automatique est **validé contre 20 items lus à la
   main**, et la règle des 5 % est appliquée à la conclusion.
6. `eval/boards.test.jsonl` **n'a pas été ouvert** ; la décision est datée au registre.
7. Aucun banc committé n'a bougé (`CommittedBenchIntegrityTests` reste vert).
8. Aucun octet de code d'évaluation dans l'image Docker — le `Dockerfile` ne référence toujours que
   `SoClover/SoClover.csproj`.

Et, au-delà des critères : le PRD est **clos**. Le prompt v5 a un chiffre, ce chiffre a une échelle
et un juge légitimé, les modes d'échec sont pondérés, et le registre contient sa première ligne
défendable.

---

## 11. Ce que ce cycle ne livre pas

- **Les interventions elles-mêmes** (rangs 1 à 6 du PRD : best-of-N, few-shot ciblé, modèle et
  échantillonnage, contexte inter-directions, RAG, SFT). Le chantier livre de quoi les **arbitrer**,
  jamais elles — c'est le non-objectif fondateur.
- Le **pack few-shot** `fewshot/pack.fr.json`, qui dérive de la séance A et relève de
  l'intervention de rang 2.
- La **première consultation du test set**, reportée au premier jalon d'intervention.
- La **langue EN** : le dispositif est transposable, mais décodeur et plafond humain sont à
  recalibrer intégralement.
- La **télémétrie de production** (`ValidateGuessingBoard`), sans intérêt tant que les joueurs IA ne
  sont pas en prod — et **prioritaire** le jour où ils le seront.
