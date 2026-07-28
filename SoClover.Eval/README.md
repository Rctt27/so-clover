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

## Ce que ce cycle ne livre pas

- **P4 / P5** — outil de saisie humaine (séance auteur chronométrée, séance juge en aveugle).
- **P6** — portes de calibration accord ≥ 75 % et κ ≥ 0,40, qui exigent `comparisons.dev.jsonl`.
- **P7** — run baseline officiel, plafond humain, taxonomie chiffrée des modes d'échec.
- Le pack few-shot (`fewshot/pack.fr.json`), qui dérive de la séance A.
