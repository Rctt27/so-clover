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
# Prérequis : le .metrics.json de saturation doit porter sur les SEULS indices `solide`
dotnet run --project SoClover.Eval -- score --run eval/runs/human-<…>.jsonl \
  --subset eval/human/elicitation.dev.jsonl --subset-outcome solide

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

> **Pourquoi le prérequis `--subset-outcome solide` est une garde, pas une convention.**
> `score` écrit toujours dans `<run>.metrics.json`, chemin fixe par run : un second `score` sur le
> même pseudo-run humain — sans le drapeau — remplace le fichier **sans rien signaler**. La porte
> serait alors évaluée sur le plafond *joué* (`pass` compris, donc plus bas) et franchie pour la
> mauvaise raison. D'où `CalibrationGates.RequireSaturationSubset` : chaque `.metrics.json` porte
> désormais sa provenance (`subsetFile`, `subsetOutcome`, **toujours** renseignés — « toutes les
> issues » s'écrit `pass,solide,tiede`, jamais `null`), et `calibrate` refuse bruyamment un fichier
> qui ne déclare pas exactement `solide`. Un `.metrics.json` antérieur à l'ajout de la provenance
> ne prouve rien : il est refusé aussi.

### L'empreinte de décodeur

`<empreinte>` = 12 hex du SHA-256 de `(modèle, prompt et sa version, température, topP,
maxOutputTokens)`. **`decodesPerClue` en est exclu** : il change la granularité de R̄, pas le
décodeur — d'où une calibration à 5 décodages et des runs à 3, sans divergence.

> **Deux `recovery` d'empreintes différentes ne se comparent pas**, au même titre que deux runs de
> `benchHash` différents. C'est ce qui a imposé le **réancrage du 2026-08-04** : les décodages
> portaient `cluePromptVersion: 1` alors que `decode-clue.md` était passé en v2 — le décodeur qui
> avait produit `recovery = 0,363` n'existait plus. Tous les runs dev ont donc été repassés au
> `decode --force`, et seules les lignes du 2026-08-04 du registre sont comparables entre elles.

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
s'affiche : une taxonomie qui classe 100 % des items est une taxonomie qui triche.

`M5` (jargon / mot rare) n'est **jamais** produit automatiquement : le harnais n'embarque aucune
ressource de fréquence lexicale, et un proxy inventé donnerait une fausse impression de rigueur.
Il se pose à la main sur l'échantillon lu.

### Le dénominateur : les directions *exploitables*, jamais le total

Une direction **D6** — ni indice valide, ni décodage exploitable — n'est pas un échec *sémantique* :
le vocabulaire fermé `M0…M6` n'a aucun code pour « échec de format du décodeur ». Elle est donc
sortie de la mesure, partout et de la même façon :

| Endroit | Traitement de D6 |
|---|---|
| parts par mode (`M0…M4`, `M?`) | ni au numérateur ni au dénominateur — dénominateur = `ScorableDirectionCount` |
| rapport | comptée à part, ligne `dont SANS décodage` (`UnscorableDirectionCount`) |
| moyenne board de `M6` | exclue de la moyenne, et un board qui perd **ne serait-ce qu'une** direction est écarté de `M6` (`M6MinExploitableDirections = 4`) — jamais imputé à 0 |
| tirage de `--sample` | exclue des candidats : la lire n'apprendrait rien sur les modes d'échec, et elle tirerait vers le bas l'accord auto ↔ humain qui gouverne le seuil 0,70 |

Imputer `R̄ = 0` à une direction sans décodage biaiserait la moyenne board à la baisse — donc des
faux négatifs `M6`. `M6` affirme que le **board lui-même** est en cause : sa moyenne se compare à
`board_positions`, qui reflète les 4 cartes. Une moyenne sur 3 directions ne se compare plus à cette
référence, quelle que soit la performance des directions restantes.

La **règle des 5 %** (`≥ 5 % → intervention justifiée`) n'est imprimée que sur les modes d'échec
sémantiques mesurés automatiquement. Deux exceptions explicites dans le rapport :
`M5` porte `non extrapolé` (jamais mesuré automatiquement), `M?` porte le rappel des directions sans
décodage. Pour ces deux-là, « ≥ 5 % » ne veut rien dire.

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

> **La séance A a été tenue le 2026-08-04** — `eval/human/elicitation.dev.jsonl`, 40 directions,
> 22 `solide` / 17 `tiede` / 1 `pass`, médiane 30 s sur les `solide`. La séance B reste à tenir ;
> son verbe `judge` sera le premier à consommer la garde J+1, désormais largement satisfaite.

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
> dénominateurs suivent — `DirectionCount` passe à 40, `parse_failure_rate` se rapporte aux seules
> tentatives du sous-ensemble — et la cellule *réglages* du registre porte `subset=<nom> (40/160)` :
> **aucune ligne ne peut prétendre porter sur le banc entier alors qu'elle porte sur un quart.**
> La même provenance part dans le `.metrics.json` (`subsetFile`, `subsetOutcome`) : c'est elle que
> `calibrate` relit pour vérifier que la porte de saturation porte bien sur les seuls `solide`.

Un `pass` reste au dénominateur (`failureKind: "pass"`, `valid: false`) : retirer les cas durs,
c'est retirer la résolution de l'instrument. `decode` saute N3 tout seul sur ce pseudo-run, aucun
board n'ayant ses 4 directions annotées.

### Le devineur humain — `guess` et `guess-report` (séances D et E)

Les séances A et B font écrire et comparer des indices ; la séance D fait **deviner**, c'est-à-dire
exactement la tâche du décodeur. C'est ce qui valide l'instrument sur ce que le jeu contient.

```bash
# Séance D — un humain devine, 16 mots à plat + 1 indice -> 2 mots, ≈ 18 min
dotnet run --project SoClover.Eval -c Release -- guess \
  --bench eval/boards.dev.jsonl --run eval/runs/<runId>.jsonl \
  --elicitation eval/human/elicitation.dev.jsonl \
  --seed 20260807001 --out eval/human/guessing.dev.jsonl

# Δ R̄ humain vs décodeur, apparié par direction (aucun appel LLM)
dotnet run --project SoClover.Eval -c Release -- guess-report \
  --guessing eval/human/guessing.dev.jsonl \
  --decoded  eval/runs/<runId>.<empreinte>.decoded.jsonl

# Séance E — un SECOND devineur, même montage, puis la dispersion H1 / H2 / décodeur
dotnet run --project SoClover.Eval -c Release -- guess-report \
  --guessing   eval/human/guessing.dev.jsonl \
  --guessing-b eval/human/guessing.e.dev.jsonl \
  --decoded    eval/runs/<runId>.<empreinte>.decoded.jsonl

# K devineurs — le drapeau se répète, le premier est H1 (la séance servie)
dotnet run --project SoClover.Eval -c Release -- guess-report \
  --guessing eval/human/guessing.dev.jsonl \
  --guessing eval/human/guessing.marie.jsonl \
  --guessing eval/human/guessing.paul.jsonl \
  --decoded  eval/runs/<runId>.<empreinte>.decoded.jsonl
```

> **`--out` est obligatoire pour toute séance après la première.** Sans lui, `guess` écrit dans
> `guessing.dev.jsonl` et **détruit** la séance déjà tenue — la seule preuve de validité de
> l'instrument. Le rapport pose un second garde-fou : deux fichiers partageant un `sessionId` sont
> refusés comme « la même séance, pas deux devineurs ».

Avec `--guessing-b`, le verbe applique le critère **relatif** de la séance E — `|Δ(H1,D)| ≤
|Δ(H1,H2)|`, « le décodeur tombe dans la dispersion humaine » — et rapporte les **trois** Δ sur le
**même** sous-ensemble de directions, celles communes aux deux séances et au décodage. Les manifestes
doivent concorder sur `benchHash`, `seed` et `runId` : la seule variable autorisée entre D et E est
*la personne*, et un montage divergent est refusé (`MismatchedBenchException`) plutôt qu'apparié
approximativement.

**À partir de trois devineurs**, la règle d'agrégation pré-enregistrée le 2026-08-09 gouverne :
**S** = moyenne des |Δ| sur les K(K−1)/2 paires humaines (*l'échelle*), **E** = moyenne des |Δ| sur les
K écarts humain↔décodeur (*la quantité*), critère **E ≤ S**. Des *moyennes* et non des maxima : un
maximum croîtrait mécaniquement avec K et ferait de « plus de devineurs » un moyen de passer le
critère. L'étage de résolution teste les paires **signées** au niveau corrigé de Bonferroni (α = 0,05/m)
— on ne teste pas S directement, parce que |Δ| a une espérance positive sous bruit pur et que « S > 0 »
serait vrai trivialement. À K = 2, la règle ancrée sur H1 reste celle qui tranche, et l'agrégat n'est
affiché que pour la continuité. Le rapport ajoute **S_kit**, restreint aux paires kit ↔ kit : écart
personne-à-personne pur, **diagnostic et jamais décisionnel**.

### Faire deviner quelqu'un d'ailleurs — `guess-kit` et `guess-import`

Le second devineur n'est presque jamais dans la pièce, et l'instrument ne se met pas en ligne pour
autant. `guess-kit` grave la séance dans **un fichier HTML autonome** : le devineur l'ouvre dans son
navigateur, sans serveur, sans réseau, sans rien installer, puis renvoie le `.jsonl` que la page lui
fait enregistrer.

```bash
# Grave le lot de la séance D dans un HTML de ~20 Kio, à envoyer tel quel
dotnet run --project SoClover.Eval -c Release -- guess-kit \
  --guessing eval/human/guessing.dev.jsonl \
  --out      eval/human/seance-e.html

# Au retour : réinjecte le rapport au format d'une séance servie
dotnet run --project SoClover.Eval -c Release -- guess-import \
  --guessing   eval/human/guessing.dev.jsonl \
  --kit-result seance-e-<kitHash>-<étiquette>-<session>.jsonl \
  --out        eval/human/guessing.e.dev.jsonl
```

**Plusieurs devineurs, chacun de son côté.** Le `kitHash` identifie le *montage* : il est le même
pour tout le monde, et ne peut donc pas distinguer deux fichiers reçus. C'est le `sessionId`, tiré au
chargement de la page (`e-<horodatage UTC>-<4 hex>`), qui porte l'unicité — il apparaît dans le nom du
fichier téléchargé et dans chaque ligne du corpus, où `RequireDistinctSessions` s'en sert pour refuser
« la même séance, pas deux devineurs ». Le devineur peut en outre saisir un prénom, **facultatif et
demandé seulement après sa dernière direction** : il préfixe le nom du fichier et remplit le champ
`label`, sans jamais entrer dans un calcul. Sans `--out`, `guess-import` écrit dans
`eval/human/guessing.<étiquette ou session>.jsonl` — un défaut fixe ferait échouer le deuxième import,
ou inviterait à écraser le premier.

**`--guessing` désigne la séance de référence, et son manifeste fait foi** — banc, run, graine et
boards exclus en sont relus tels quels. Recalculer ces exclusions depuis la ligne de commande a
produit 53 directions là où H1 en avait devinées 42 : trois boards avaient été exclus à la main
pendant un diagnostic, et aucun argument de la CLI ne s'en souvient.

Ce que le kit garantit :

- **la même page** — il est assemblé par substitution de la seule région `// transport:start` …
  `// transport:end` de `guess.html`, qui ne contient que `api()` et `post()`. Tout le reste — CSS,
  `render()`, `toggle()`, la touche Entrée — est recopié octet pour octet, et `GuessKitPageTests` le
  vérifie ;
- **le même aveuglement** — ni `boardId`, ni paire de référence, ni score dans le fichier : `r` naît
  à l'import, sur la machine de l'opérateur ;
- **le même montage au retour** — `kitHash` (empreinte des items, indices *et* ordre de
  présentation) est confronté au plan reconstruit, et chaque mot rapporté doit avoir été présenté.
  Un rapport venu d'un autre kit est refusé, en-tête recopié ou non ;
- **rien d'écrasé** — `guess-import` refuse un `--out` qui existe déjà.

Ce que le kit **ne** garantit pas, et qui doit être consigné : le plan entier est dans la page. Un
devineur qui ouvrirait les outils de développement verrait les items à venir — jamais les réponses,
mais assez pour défaire la dispersion des directions d'un même board (`SpaceOut`). C'est la seule
chose que le serveur assurait et que le hors-ligne ne peut pas assurer. Sauvegarde par
`localStorage` (reprise après fermeture d'onglet), avec repli visible si le navigateur la refuse.

## Ce que le harnais ne peut pas observer

`providerModelListHash` capture la liste de modèles servie par le provider, mais **pas** le toggle
« enable thinking » de LM Studio, appliqué au chargement du modèle — précisément le réglage qui a
déjà fait dériver des runs. D'où `--notes "thinking OFF, ctx 16k"`, recopié dans la ligne du
registre. Le harnais ne peut pas rendre ce réglage observable ; il peut rendre son absence visible.

## État courant — décodeur v2, au 2026-08-04

| Élément | Valeur |
|---|---|
| Banc dev | `eval/boards.dev.jsonl` — 40 boards / 160 directions, seed `20260726001`, hash `416b819a41a1` |
| Banc test | `eval/boards.test.jsonl` — 60 boards / 240 directions, seed `20260726002`, hash `1436bb07dc0d` — **jamais consulté à ce jour** |
| Décodeur | `qwen/qwen3-8b`, thinking OFF, prompt `decode-clue.md` **v2** |
| Plancher aléatoire | `recovery = 0,128` — porte `≤ 0,15` : **franchie** |
| Baseline v5 | `20260728-v5-…-d79a63b9`, temp 1,0 — `recovery = 0,370`, `half_rate = 0,630` |
| 2ᵉ run générateur | `20260804-v5-…-fbb92760`, **temp 0,7** — `recovery = 0,383`. Matériau de **contraste** pour la séance B, pas une optimisation |
| Séance A (P4) | **tenue le 2026-08-04** — 40 directions, 22 `solide` / 17 `tiede` / 1 `pass`. Corpus committé : `eval/human/elicitation.dev.jsonl` |
| Plafond humain joué | `recovery = 0,333` sur 40/160 directions (toutes issues, `pass` compris) |
| Plafond humain `solide` | `recovery = 0,341` sur 22/160 directions (A-3) |
| Séance B (P5) | **non tenue** — sans elle, ni accord, ni κ, ni calibration |
| Statut du registre | `pré-calibration` sur **toutes** les lignes — seule la porte du plancher est franchie |

Le plancher mesuré colle à la valeur théorique du hasard pur : tirer 2 mots parmi 16 donne une
intersection espérée de `2 × 2/16 = 0,25` mot, soit `R̄ = 0,125`. Le décodeur ne devine donc rien à
partir d'indices aléatoires — c'est précisément ce que la porte vérifie.

Deux runs générateurs distincts existent désormais sur le banc dev : c'est le **prérequis de la
séance B**, faute de quoi la famille `modelVsModel` retomberait sur le plancher aléatoire.

### Ce que le premier baseline a montré

Chiffres du **décodeur v1**, conservés pour la lecture qualitative — ils ne se comparent pas à ceux
du tableau ci-dessus, d'empreinte différente.

| Indicateur | v5 | Plancher |
|---|---|---|
| `valid_rate` | 0,963 | 1,000 |
| `first_attempt_rate` | 0,963 | 1,000 |
| `parse_failure_rate` | 0,031 | 0,000 |
| `recovery` | **0,363** | 0,130 |
| `strict_2of2_all_decodes` | 0,013 | 0,000 |
| `half_rate` | **0,656** | 0,225 |
| `board_positions` | 0,278 | 0,193 |
| `board_solved_first_try` | 0,000 | 0,000 |
| `decode_failure_rate` | 0,044 | 0,035 |

Le mode d'échec dominant est net et chiffré : `half_rate = 0,656` contre
`strict_2of2_all_decodes = 0,013`.
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

### `strict_2of2_all_decodes` : un critère d'unanimité, pas un taux de succès

`RunMetrics.Compute` compte une direction dans cette métrique seulement si **les trois décodages**
sont à `r = 1` (`Scoring/RunMetrics.cs`, `scored.All(d => d.R!.Value == 1.0)`). L'ancien nom
`strict_2of2` suggérait « les 2 mots sur 2 retrouvés » ; la mesure exige en réalité que le décodeur
y parvienne trois fois de suite — d'où le renommage, **à l'affichage et au registre uniquement** :
la clé sérialisée `strict2Of2` des `.metrics.json` reste intacte, les 18 colonnes du registre aussi.
Même remède que `board_solved_first_try` : la contrainte est portée par le nom, pas découverte
après coup. Deux conséquences, toutes deux vérifiées sur les runs du 2026-08-04.

**Un `strict_2of2_all_decodes` nul ne veut pas dire que le décodeur échoue toujours.** Sur les 22
indices humains `solide`, il vaut `0,000` alors que **9,1 % des décodages** sont à `r = 1` — taux
supérieur à celui du modèle v5 (6,3 %). Quatre directions ont eu au moins un décodage parfait,
deux d'entre elles à 2 sur 3. Ce qui manque n'est pas la réussite, c'est sa stabilité.

**Sur un petit dénominateur, la métrique n'a presque aucune résolution.** Sur 22 directions elle ne
peut valoir que 0 ; 0,045 ; 0,091… Avec ~9 % de réussite par décodage, l'unanimité 3/3 est rare par
construction : observer 0 est le résultat attendu, pas un signal. Sur le pseudo-run humain
(`--subset`, 22 à 40 directions), **ne rien conclure de cette métrique**.

Pour juger la devinabilité complète, lire la **distribution brute des `r`** dans le
`.decoded.jsonl`, pas cet agrégat. C'est là qu'apparaît le fait intéressant du corpus humain : les
indices humains sont plus polarisés que ceux du modèle — plus de `r = 0` (40,9 % contre 30,0 %)
mais plus de `r = 1` — quand le modèle se masse sur le demi-succès (`half_rate` 0,630 contre 0,455
sur les `solide`).

### Lire un taux avec ses effectifs

C'est ce piège, généralisé, qui a mis les effectifs derrière chaque taux à l'affichage de `score` :

```
  strict_2of2_all_decodes 0,000   (0/22)   ← unanimité des 3 décodages
  half_rate              0,455   (10/22)
```

Un taux seul se lit comme un fait ; sur un petit dénominateur il n'est parfois qu'un plancher
d'estimateur. Dénominateur nul : **aucun taux n'est imprimé** (`—`) — un `0,000` sur zéro item
serait un chiffre entièrement fabriqué. Ces compteurs vivent dans `MetricCounts`, à l'affichage
seulement : **aucune colonne de registre n'est créée**, les 18 colonnes sont préservées.

Un dénominateur suit désormais `--subset` jusqu'au bout : `parse_failure_rate` se rapporte aux
tentatives **du sous-ensemble** (`scopedAttempts`), pas du run entier — sinon le taux serait divisé
par la part du banc couverte, soit un quart sur le pseudo-run humain.

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