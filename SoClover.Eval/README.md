# SoClover.Eval — harnais d'évaluation des indices IA

Projet console **hors ligne, jamais déployé**. Le `Dockerfile` de `SoClover` ne référence que
`SoClover/SoClover.csproj` et `docs/deploy.md` fait `git archive HEAD … SoClover/` : aucun octet
de ce projet n'atteint la production.

Spécifications : [`Specs/AI_Clue_Eval_Loop/`](../Specs/AI_Clue_Eval_Loop/).

## Configuration

`evalsettings.json` porte deux sections, `Generator` et `Decoder`, **chacune de la forme
`LlmOptions`** — validées par le `LlmOptionsValidator` de production. Surcharges par variables
d'environnement : `GENERATOR__DEFAULTMODEL`, `DECODER__BASEURL`, etc.

> **Pourquoi pas `appsettings.json`** — les items `Content` d'un projet se propagent
> transitivement à ses référençants (c'est ce mécanisme qui apporte ici les prompts et les
> dictionnaires de `SoClover`). Sous le nom `appsettings.json`, ce fichier gagnait la course de
> copie contre celui de `SoClover` dans l'output de `SoClover.Tests` et faisait disparaître
> `GameDefaults` / `Llm` / `AIPlayers` des tests d'intégration HTTP, silencieusement. Un nom
> distinct supprime la collision à la racine ; le test
> `EvalLlmConfigTests.BuildConfiguration_reads_evalsettings_without_shadowing_the_production_appsettings`
> verrouille l'invariant.

Les secrets ne sont **jamais** committés. Pour un provider payant :

```bash
GENERATOR__APIKEY=sk-ant-... dotnet run --project SoClover.Eval -- generate ...
```

Un `evalsettings.local.json` (gitignoré) peut porter des surcharges locales.

## Contrainte structurante : deux passes

LM Studio ne sert qu'un modèle à la fois. Générer et décoder sont donc deux commandes distinctes,
séparées par un **rechargement manuel de modèle** :

```bash
# 1. Charger le modèle générateur dans LM Studio, thinking OFF, contexte ≥ 12k
dotnet run --project SoClover.Eval -- generate --bench eval/boards.dev.jsonl --notes "thinking OFF, ctx 16k"

# 2. Charger le modèle décodeur dans LM Studio
dotnet run --project SoClover.Eval -- decode --run eval/runs/<runId>.jsonl

# 3. Scorer (aucun appel LLM)
dotnet run --project SoClover.Eval -- score --run eval/runs/<runId>.jsonl --ledger eval/LEDGER.md
```

Les deux commandes sont **reprenables** : les relancer complète ce qui manque, `--force` repart
de zéro.

## Comparer deux runs

```bash
dotnet run --project SoClover.Eval -- compare \
  --baseline eval/runs/<runIdA>.jsonl \
  --variant  eval/runs/<runIdB>.jsonl
```

Rend le Δ`recovery` **apparié** (moyenne des différences item par item), son intervalle de
confiance bootstrap à 95 %, et le verdict de la règle de promotion (`retenu` / `neutre` /
`écarté`). Deux runs de `benchHash` différents sont **refusés** : comparer sur deux tirages
distincts est une erreur de protocole, pas une approximation acceptable.

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

## Séances humaines (P4-P5)

Le `recovery` automatique a un bas d'échelle (le plancher aléatoire) et **pas de haut** : rien ne
dit s'il reste de la marge, ni si le décodeur juge comme un joueur humain. Les deux séances
produisent ces deux corpus manquants. Design :
[`02_Design_Human_P4_P5.md`](../Specs/AI_Clue_Eval_Loop/02_Design_Human_P4_P5.md).

Chaque verbe démarre un `WebApplication` sur `127.0.0.1` servant une page unique. **Le serveur
applique le protocole** — c'est tout l'intérêt de la forme : une page statique ne pourrait garantir
ni le verrou A-4, ni l'aveuglement, ni le chrono mesuré côté serveur.

```bash
# Séance A — auteur, chronométrée, ~40 directions, ≈ 1 h 30
dotnet run --project SoClover.Eval -- elicit \
  --bench eval/boards.dev.jsonl --seed 20260729001 \
  --candidates-run eval/runs/<runId>.jsonl \
  --out eval/human/elicitation.dev.jsonl

# ATTENDRE 24 H — la garde A-5 du verbe judge le rappellera

# Séance B — juge, en aveugle, ~100 couples, ≈ 45 min
dotnet run --project SoClover.Eval -- judge \
  --bench eval/boards.dev.jsonl --seed 20260730001 \
  --run-a eval/runs/<runA>.jsonl --run-b eval/runs/<runB>.jsonl \
  --anchor-run eval/runs/<plancher>.jsonl \
  --out eval/human/comparisons.dev.jsonl

# Agrégats des deux séances (aucun appel LLM, ni accord ni κ — c'est P6)
dotnet run --project SoClover.Eval -- human-report \
  --elicitation eval/human/elicitation.dev.jsonl \
  --comparisons eval/human/comparisons.dev.jsonl
```

Les deux séances sont **reprenables** : chaque item est écrit à la soumission, relancer le verbe
complète ce qui manque. `eval/human/` est **committé**, contrairement à `eval/runs/`.

Trois règles sont non contournables par construction, pas par discipline : `GET /api/candidates`
répond `409` tant que l'item n'a pas été tenté (A-4) ; il n'existe **aucune API de saut**, un item
ne se quitte que par `solide`, `tiede` ou `pass` (A-1) ; la réponse de `/api/next` de la séance B ne
contient jamais `source` ni `runId`, la provenance étant réattachée côté serveur à l'écriture.

Prérequis de la séance B : **deux runs générateurs** sur le banc dev. Avec un seul run réel, la
famille `modelVsModel` retomberait sur le plancher aléatoire, dont l'écart de qualité évident
gonflerait artificiellement l'accord et κ en P6.

### Le plafond humain — `human-run` et `--subset`

```bash
# 1. La séance A devient un pseudo-run au format RunFile
dotnet run --project SoClover.Eval -- human-run --elicitation eval/human/elicitation.dev.jsonl

# 2. Décoder avec LE MÊME décodeur que les runs auxquels on le comparera
dotnet run --project SoClover.Eval -- decode --run eval/runs/human-<date>-<hash8>.jsonl

# 3. Plafond joué (toutes issues, pass compris)
dotnet run --project SoClover.Eval -- score --run eval/runs/human-<…>.jsonl \
  --subset eval/human/elicitation.dev.jsonl --ledger eval/LEDGER.md

# 3 bis. Plafond sur les seules paires résolues (A-3)
dotnet run --project SoClover.Eval -- score --run eval/runs/human-<…>.jsonl \
  --subset eval/human/elicitation.dev.jsonl --subset-outcome solide,tiede

# 4. Écart apparié modèle ↔ humain, avec son IC
dotnet run --project SoClover.Eval -- compare \
  --baseline eval/runs/<run v5>.jsonl --variant eval/runs/human-<…>.jsonl \
  --subset eval/human/elicitation.dev.jsonl
```

> **`--subset` n'est pas un confort, c'est une correction de dénominateur.** `RunMetrics.Compute`
> construit ses items depuis le banc **entier** et attribue `R̄ = 0` aux directions absentes —
> comportement voulu pour un run de modèle, où une direction non générée est un échec. Appliqué tel
> quel à un run humain couvrant 40 directions sur 160, il produirait deux chiffres faux et
> parfaitement plausibles : un plafond humain **divisé par quatre**, et un `compare` appariant
> l'humain au modèle sur 120 items où l'humain n'a jamais rien écrit. Avec le drapeau, tous les
> dénominateurs suivent, `DirectionCount` passe à 40, et la cellule *réglages* du registre porte
> `subset=<nom> (40/160)` : **aucune ligne ne peut prétendre porter sur le banc entier alors
> qu'elle porte sur un quart.**

Un `pass` reste au dénominateur (`failureKind: "pass"`, `valid: false`) : retirer les cas durs,
c'est retirer la résolution de l'instrument. `decode` saute N3 tout seul sur ce pseudo-run, aucun
board n'ayant ses 4 directions annotées.

## Ce que le harnais ne peut pas observer

`providerModelListHash` capture la liste de modèles servie par le provider, mais **pas** le toggle
« enable thinking » de LM Studio, appliqué au chargement du modèle — précisément le réglage qui a
déjà fait dériver des runs. D'où `--notes "thinking OFF, ctx 16k"`, recopié dans la ligne du
registre. Le harnais ne peut pas rendre ce réglage observable ; il peut rendre son absence visible.

## État à la clôture du cycle P0-P3

| Élément | Valeur |
|---|---|
| Banc dev | `eval/boards.dev.jsonl` — 40 boards / 160 directions, seed `20260726001`, hash `416b819a41a1` |
| Banc test | `eval/boards.test.jsonl` — 60 boards / 240 directions, seed `20260726002`, hash `1436bb07dc0d` — **non consulté dans ce cycle** |
| Décodeur | `qwen/qwen3-8b`, thinking OFF, temp 0,3, `maxOutputTokens` 512 — 480 décodages N2 + 40 N3 en 3 min 11 s |
| Plancher aléatoire | `recovery = 0,130` — porte `≤ 0,15` : **franchie**. `decode_failure_rate = 0,035` (≤ 0,05) |
| Run baseline | `20260728-v5-google-gemma-4-12b-qat-d79a63b9` — prompt FR PerDirection v5, `google/gemma-4-12b-qat`, 160 directions en 10 min 44 |
| `recovery` baseline | **0,363** — soit `+23,3` pts sur le plancher, IC 95 % `[+19,3 ; +27,3]` |
| Statut du registre | `pré-calibration` — les portes P6 (accord ≥ 75 %, κ ≥ 0,40) ne sont pas franchies |

Le plancher mesuré (`0,130`) colle à la valeur théorique du hasard pur : tirer 2 mots parmi 16
donne une intersection espérée de `2 × 2/16 = 0,25` mot, soit `R̄ = 0,125`. Le décodeur ne devine
donc rien à partir d'indices aléatoires — c'est précisément ce que la porte vérifie.

### Ce que le premier baseline montre

| Indicateur | v5 | Plancher |
|---|---|---|
| `valid_rate` | 0,963 | 1,000 |
| `first_attempt_rate` | 0,963 | 1,000 |
| `parse_failure_rate` | 0,031 | 0,000 |
| `recovery` | **0,363** | 0,130 |
| `strict_2of2` | 0,013 | 0,000 |
| `half_rate` | **0,656** | 0,225 |
| `board_positions` | 0,278 | 0,193 |
| `board_solved_first_try` | 0,000 | 0,000 |
| `decode_failure_rate` | 0,044 | 0,035 |

Le mode d'échec dominant est net et chiffré : `half_rate = 0,656` contre `strict_2of2 = 0,013`.
Dans deux tiers des directions, le décodeur retrouve **un seul** des deux mots visés, et presque
jamais les deux. C'est la « signature Hôpital » du PRD — l'indice s'accroche fortement à un mot et
laisse l'autre orphelin, au lieu de tendre un pont entre les deux. `board_solved_first_try = 0,000`
sur les 40 boards en découle mécaniquement.

### N3 : piloter sur `board_positions`, pas sur `board_solved_first_try`

`board_solved_first_try` vaut **0,000 des deux côtés** — pour le prompt v5 comme pour le plancher
aléatoire. Une métrique qui ne bouge pas entre un pipeline réel et du bruit ne discrimine rien :
elle ne pourra jamais départager un v5 d'un v6. **Ne pas l'utiliser comme critère de décision.**
Elle reste au registre comme témoin, et dans la règle de promotion uniquement comme garde-fou de
régression (≤ 5 pts), ce qui est inoffensif tant qu'elle est à zéro.

Deux raisons à ce zéro, et aucune n'est « le modèle est mauvais » :

1. **L'exigence est irréaliste.** La métrique demande les 8 slots corrects **du premier coup**. Le
   jeu réel accorde 3 tentatives, avec annonce des positions correctes entre chaque
   (`Game.RemainingAttempts`, `Domain/Game.cs:74`) — et même des joueurs expérimentés résolvent
   rarement un plateau d'emblée. La métrique mesure une exigence que personne ne satisfait, pas
   une qualité d'indice. C'est le sens du renommage : le nom porte désormais la contrainte.
2. **Le chiffre est déjà une borne supérieure optimiste.** `BoardDecoder` mesure l'affectation
   mot → arête ; le vrai jeu fait placer des *cartes* avec la bonne rotation, ce qui engage aussi
   les 8 faces intérieures. Le score réel serait donc encore plus bas.

**Le signal N3 exploitable est `board_positions`** (0,278 contre 0,193), gradué et non saturé. Il
sépare toutefois bien moins que `recovery` (+8,5 pts contre +23,3 pts) : N3 reste un indicateur de
diagnostic — détecter le mode `M6`, collision inter-directions — et non un critère de promotion.

> **Pourquoi le harnais ne rejoue pas les 3 tentatives.** Le PRD l'exclut explicitement
> (`00_Overview.md:152`, « du premier coup ») et range la boucle multi-tentatives en hors-périmètre,
> côté télémétrie de production. La raison est méthodologique : avec le feedback « ces positions
> sont bonnes », un décodeur converge **par élimination** même sur des indices médiocres. On
> mesurerait un mélange de qualité d'indice et de capacité de déduction du décodeur — exactement ce
> que le harnais cherche à isoler. Le PRD identifie d'ailleurs cette stratégie et la range du côté
> humain : « stratégie au niveau du board, structurellement hors d'atteinte de `PerDirection` »
> (`00_Overview.md:441-444`).
>
> La mesure fidèle au jeu réel viendra de `ValidateGuessingBoard`
> (`UseCases/Gameplay/ValidateGuessingBoard.cs`), qui calcule déjà la correction par position, à
> chaque tentative, par de vrais joueurs, avec les vraies rotations — puis la jette.

> **Attention à l'interprétation de `compare` sur ce couple.** Comparer v5 au plancher rend un
> verdict `ÉCARTÉ`, motivé par `Δ valid_rate = -3,8 pts`. Ce n'est **pas** un jugement sur v5 :
> le plancher a un `valid_rate` de 1,000 par construction (`RandomBaselineRunner` ne retient qu'un
> mot déjà validé par `ClueAcceptance`), donc toute génération réelle perd forcément du terrain
> sur cette colonne. La règle de promotion est faite pour départager **deux variantes de prompt**,
> pas un prompt et un plancher. Le chiffre à lire ici est le Δ`recovery` et son IC, qui ne
> contient pas zéro.

> **Aucune ligne du registre n'est défendable avant P6.** Les chiffres de ce cycle sont
> techniquement valides et pas encore légitimés : seule la porte du plancher aléatoire a été
> franchie.

### Reproduire un cycle complet

**Le rechargement manuel de modèle n'est pas nécessaire avec LM Studio récent** : les deux modèles
sont servis simultanément par chargement JIT, `generate` et `decode` s'enchaînent sans
intervention. La contrainte des deux passes reste vraie pour un serveur qui n'expose qu'un modèle
à la fois. Vérifier ce qui est servi : `curl http://localhost:1234/v1/models`.

Renseigner dans `evalsettings.json` les identifiants **exacts** rendus par cet endpoint — LM Studio
préfixe l'organisation (`google/gemma-4-12b-qat`, et non `gemma-4-12b-qat`). Un identifiant qui ne
correspond à rien fait échouer le premier appel.

1. Charger les modèles, **thinking OFF** sur les deux, contexte ≥ 12k (générateur) et ≥ 8k
   (décodeur). Vérifier les prompts : `dotnet run --project SoClover.Eval -- doctor`.
2. `generate --bench eval/boards.dev.jsonl --notes "thinking OFF, ctx 16k"` — 160 directions,
   ~11 min sur un 12B local. Reprenable : relancer la même commande après une interruption.
3. `decode --run eval/runs/<runId>.jsonl --decodes 3` — 480 décodages N2 + 40 N3, ~2-3 min sur un
   8B local.
4. `score --run … --ledger eval/LEDGER.md --hypothesis "…" --decision neutre`.
   **Vérifier `decode_failure_rate ≤ 0,05`** — au-delà, le prompt décodeur est cassé et aucun
   `recovery` n'est lisible.
5. Décoder et scorer le plancher aléatoire avec le **même décodeur**. **Porte : `recovery ≤ 0,15`.**
   Si elle n'est pas franchie, le décodeur devine à partir de rien : consigner l'échec au registre
   (`--decision écarté`) et reprendre `decode-clue.md` avant de publier le moindre `recovery`.
6. `compare --baseline <runA> --variant <runB>` pour le Δ apparié et son IC.

## Ce que les cycles livrés ne livrent pas

L'outillage P4-P5 est livré ; **les deux séances restent à tenir**, et sans elles aucun corpus
humain n'existe. Le code ne produit rien tant que l'opérateur n'a pas saisi.

- **P6** — accord décodeur/humain et κ de Cohen, portes ≥ 75 % et ≥ 0,40. Les deux corpus et le
  pont (`human-run`, `--subset`) sont dimensionnés pour, mais **aucun chiffre de décodeur ne
  devient défendable avant**.
- **P7** — run baseline officiel, plafond humain publié, taxonomie chiffrée des modes d'échec.
- Le pack few-shot (`fewshot/pack.fr.json`), qui dérive de la séance A.
