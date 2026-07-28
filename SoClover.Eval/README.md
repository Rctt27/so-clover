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
| Run baseline | *à produire — voir « Run de clôture » ci-dessous* |
| `recovery` baseline | *à produire* |
| Statut du registre | `pré-calibration` — les portes P6 (accord ≥ 75 %, κ ≥ 0,40) ne sont pas franchies |

Le plancher mesuré (`0,130`) colle à la valeur théorique du hasard pur : tirer 2 mots parmi 16
donne une intersection espérée de `2 × 2/16 = 0,25` mot, soit `R̄ = 0,125`. Le décodeur ne devine
donc rien à partir d'indices aléatoires — c'est précisément ce que la porte vérifie.

> **Aucune ligne du registre n'est défendable avant P6.** Les chiffres de ce cycle sont
> techniquement valides et pas encore légitimés : seule la porte du plancher aléatoire a été
> franchie.

### Run de clôture — procédure opérateur

Le code des cinq verbes est complet et testé ; les chiffres réels demandent LM Studio et deux
modèles distincts, chargés successivement. Dans l'ordre :

1. Charger le **générateur** (`Generator.defaultModel` dans `evalsettings.json`), thinking OFF,
   contexte ≥ 12k. Vérifier : `dotnet run --project SoClover.Eval -- doctor`.
2. `generate --bench eval/boards.dev.jsonl --notes "thinking OFF, ctx 16k"` — 160 directions,
   plusieurs heures en séquentiel local. Reprenable : relancer la même commande après une
   interruption.
3. Recharger LM Studio avec le **décodeur** (famille différente du générateur), thinking OFF,
   contexte ≥ 8k. Puis `decode --run eval/runs/<runId>.jsonl --decodes 3`.
4. `score --run … --ledger eval/LEDGER.md --hypothesis "…" --decision neutre`.
   **Vérifier `decode_failure_rate ≤ 0,05`** — au-delà, le prompt décodeur est cassé et aucun
   `recovery` n'est lisible.
5. Décoder et scorer le plancher aléatoire avec le **même décodeur**. **Porte : `recovery ≤ 0,15`.**
   Si elle n'est pas franchie, le décodeur devine à partir de rien : consigner l'échec au registre
   (`--decision écarté`) et reprendre `decode-clue.md` avant de publier le moindre `recovery`.
6. `compare --baseline <plancher> --variant <baseline>` pour vérifier le Δ apparié et son IC.

## Ce que ce cycle ne livre pas

- **P4 / P5** — outil de saisie humaine (séance auteur chronométrée, séance juge en aveugle).
- **P6** — portes de calibration accord ≥ 75 % et κ ≥ 0,40, qui exigent `comparisons.dev.jsonl`.
- **P7** — run baseline officiel, plafond humain, taxonomie chiffrée des modes d'échec.
- Le pack few-shot (`fewshot/pack.fr.json`), qui dérive de la séance A.
