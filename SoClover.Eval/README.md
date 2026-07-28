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

## Ce que le harnais ne peut pas observer

`providerModelListHash` capture la liste de modèles servie par le provider, mais **pas** le toggle
« enable thinking » de LM Studio, appliqué au chargement du modèle — précisément le réglage qui a
déjà fait dériver des runs. D'où `--notes "thinking OFF, ctx 16k"`, recopié dans la ligne du
registre. Le harnais ne peut pas rendre ce réglage observable ; il peut rendre son absence visible.
