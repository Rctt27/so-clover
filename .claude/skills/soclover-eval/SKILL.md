---
name: soclover-eval
description: Conduit toute phase de test, mesure ou comparaison de LLM pour les joueurs IA de SoClover — harnais SoClover.Eval, bancs eval/, registre eval/LEDGER.md. Utilise cette skill dès qu'il s'agit d'évaluer, comparer, calibrer ou régler un modèle : changer de modèle générateur ou décodeur, lancer generate/decode/score/calibrate/analyze, charger un modèle dans LM Studio pour un test, toucher aux prompts d'indices ou au prompt décodeur, lire ou écrire une ligne du registre, interpréter un recovery / accord / kappa / les quatre portes, ou modifier le code de SoClover.Eval. Déclenche-toi même sans le mot « éval » — « je viens de télécharger tel modèle, on le teste ? », « pourquoi le score a baissé ? », « on relance un décodage ? », « est-ce que ce prompt est meilleur ? » relèvent tous de cette skill.
---

# Tester un LLM pour les joueurs IA de SoClover

Le harnais `SoClover.Eval` mesure la qualité des indices produits par un LLM **générateur** en
faisant deviner ces indices par un LLM **décodeur**. Le taux de récupération (`recovery`, R̄) est le
score. Avant qu'un `recovery` veuille dire quoi que ce soit, l'instrument lui-même doit être validé
contre un corpus de jugements humains — c'est la phase P6 et ses **quatre portes**.

## Qui dit quoi — ne pas dupliquer

| Source | Contenu | Autorité sur |
|---|---|---|
| `Specs/AI_Clue_Eval_Loop/` | PRD + design P0-P7 | **ce que le harnais doit faire** |
| `SoClover.Eval/README.md` | mode d'emploi des verbes, drapeaux | **comment on l'appelle** |
| `eval/LEDGER.md` | une ligne par run et par calibration, append-only | **ce qu'on a mesuré** |
| cette skill | protocole, gardes, pièges d'artefacts | **comment on conduit un test** |

Chiffres, verdicts et rétractations vivent dans le registre, **jamais ici**. Cette skill ne se
met pas à jour après un run ; elle se met à jour quand la *méthode* change.

**Ne pas re-dériver ce que ce tableau attribue déjà.** Le circuit des verbes, les gardes et les
pièges d'artefacts sont *ici* : rouvrir `README.md` ou le code de `SoClover.Eval` pour les
retrouver est du gaspillage, pas de la rigueur. Lire un artefact se justifie pour les **valeurs**
d'une mesure passée, que rien d'autre ne porte — l'`--epsilon` et le `decodesPerClue` d'une
calibration à reproduire (dans son `.json`), la granularité d'un run d'ancrage (manifeste de son
`.decoded.jsonl`), une empreinte. Règle courte : **le protocole se lit ici, les paramètres se
lisent dans les artefacts.** Et écrire une note de registre ne demande **ni build ni
`dotnet test`** — un fichier Markdown ne compile pas.

## Avant de commencer — trois lectures

1. La **dernière ligne** de `eval/LEDGER.md` (table des runs *et* table des calibrations) et les
   notes de synthèse en fin de fichier. Elles portent les rétractations : une conclusion écrite
   plus haut peut avoir été abandonnée plus bas.
2. `git log --oneline -15` sur la branche courante.
3. La mémoire `project-eval-decoder-v2-reancrage` si elle est chargée.

## Pré-vol LM Studio

- **Un seul modèle est servi à la fois.** `generate` et `decode` sont donc deux passes séparées par
  un **rechargement manuel** par l'opérateur. Les deux verbes sont reprenables ; `--force` repart
  de zéro.
- **Le toggle « enable thinking » est appliqué au chargement du modèle** et n'apparaît dans aucun
  champ observable. Le consigner dans `--notes "thinking OFF, ctx 16k"`, recopié dans le registre.
  Pour un modèle annoncé « reasoning », **vérifier** : `reasoning_tokens = 0` dans l'usage prouve
  que le mode est inactif quel que soit le nom du modèle.
- **Contexte** : ≥ 12k côté générateur. Le décodeur tient à 4k (prompt ~385 tokens).
- **Les réglages de l'UI sont des défauts, pas des overrides.** Vérifié empiriquement le
  2026-08-06 : la température de la requête gagne. Le vrai piège est l'inverse — `ChatOptions` ne
  transmet que `ModelId`, `Temperature`, `TopP`, `MaxOutputTokens`. `top_k`, `repeat_penalty` et
  `min_p` viennent des réglages **par modèle** de l'UI et sont invisibles dans tout artefact :
  `--notes`, ou rien ne les retrouvera.
- `quantization` et `loadedContextLength` sont sondés automatiquement (`ModelRuntimeProbe`,
  `/api/v0/models`) et écrits aux manifestes, **hors empreinte**.

## Le circuit d'un test

Toutes les commandes se lancent **depuis la racine du dépôt**, jamais depuis `SoClover.Eval/`
(sinon MSB1009, et les chemins `eval/…` ne résolvent pas) :

```
dotnet run --project SoClover.Eval -c Release -- <verbe> [options]
```

```
doctor → generate → [rechargement manuel du modèle] → decode → score → calibrate → ligne LEDGER → commit
```

- `bench` **ne se relance pas** : `eval/boards.dev.jsonl` (40 boards) et `boards.test.jsonl` (60)
  sont committés avec leur seed et leur hash. Un banc qui bouge invalide tout l'historique du
  registre, et `BenchFile.Read` refuse un banc dérivé.
- **Itérer exclusivement sur `boards.dev.jsonl`.** Le test set se consulte une fois par jalon, et
  chaque consultation se consigne dans le registre.
- **Toujours passer `--ledger`.** `eval/runs/` est gitignoré : une ligne de registre non écrite est
  une mesure perdue.
- **Contrainte pratique** : `-c Release` verrouille `bin/Release` pendant toute la séquence — on ne
  peut ni éditer ni recompiler tant qu'un run tourne. Prévoir les correctifs de code *avant* de
  lancer, ou attendre la fin.

## Séances humaines (P4-P5)

`elicit` (séance A — l'auteur écrit des indices, chronométré) et `judge` (séance B — le juge compare
des couples, en aveugle) démarrent un `WebApplication` local. **C'est le serveur qui applique le
protocole, pas la discipline de l'opérateur** — ne jamais contourner :

- verrou A-4 : `/api/candidates` renvoie `409` avant toute tentative ;
- aucune API de saut (A-1) ;
- garde J+1 d'A-5, contournable seulement par `--force-early`, qui **stampe l'entorse dans le
  manifeste** ;
- aveuglement structurel : `/api/next` ne porte ni `source` ni `runId`.

`human-run` projette la séance A en pseudo-run scorable — et c'est là que `--subset` devient
obligatoire, sinon le plafond humain est divisé par quatre.

La **séance A tourne en premier** : le plan de la séance B filtre les couples d'indices identiques,
tire `modelVsModel` **hors** des directions annotées par A, ajoute 5 ancres et 10 doublons inversés
(désormais entrelacés — les avoir laissés en queue a contaminé la cohérence intra-juge le
2026-08-06).

## Les gardes — l'acquis le plus cher du chantier

Ces règles existent parce que chacune a été enfreinte au moins une fois, avec une conclusion fausse
à la clé.

0. **Toucher au contenu d'un prompt décodeur ⟹ bumper son `version:` dans le même geste.** Aucun
   test ne le vérifie : les deux assertions littérales qui existaient (`ClueDecoderTests`,
   `BoardDecoderTests`) ne cassaient que sur un bump *volontaire*, jamais sur l'oubli, et ont été
   supprimées comme cérémonie sans valeur. Le garde-fou est ici, et nulle part ailleurs. Ce qu'on
   risque en l'oubliant est l'incident fondateur du cycle P6 : `DecoderFingerprint` ne hache que le
   *chemin* du prompt et sa version **déclarée**, jamais son contenu — un prompt modifié sans bump
   garde donc son empreinte, deux décodeurs différents écrivent sous la même identité, et les
   `recovery` cessent d'être comparables sans qu'aucun artefact ne le signale. Vérifier le
   frontmatter **avant** de lancer `decode` ou `calibrate` ; l'en-tête de sortie affiche
   `prompt : clue vN`, le lire.
1. **Une variable à la fois** (discipline P3) : prompt → modèle → température/maxOutputTokens →
   `decodesPerClue`. Si deux bougent malgré tout, les effets ne sont **pas séparables** : le
   déclarer comme une dette dans la note de registre, et la solder par une mesure dédiée.
2. **Avant d'attribuer un écart à une cause, calculer l'écart-type d'échantillonnage attendu**
   (≈ √(p(1−p)/n) ; sur n ≈ 47 et p ≈ 0,58 il vaut 0,072). Un écart inférieur à ~2 σ n'est pas un
   effet, c'est du bruit. Une arithmétique juste sur un écart de bruit produit quand même une
   conclusion fausse.
3. **Deux mesures dont les IC se recouvrent ne se classent pas.** « A est meilleur que B » demande
   des intervalles disjoints, pas une moyenne plus haute.
4. **Distinguer échec démontré et échec indécidable** : IC entier sous le seuil ⟹ démontré ; IC
   recouvrant le seuil ⟹ indécidable au bruit actuel. Les deux ne s'agrègent pas dans une série.
5. **Ne jamais régler en inspectant les désaccords couple par couple** — c'est du surajustement au
   lot, et ça détruit la valeur du corpus.
6. **Un seuil ne se baisse pas parce qu'il gêne.** Si une porte semble mal posée, il faut le
   *mesurer* (p. ex. l'accord inter-juges pour la porte à 0,75) **avant** d'en rediscuter, et sur
   le même corpus. Trancher après avoir vu les échecs est la faute qu'on refuse.
7. **Un corpus plus grand sert à mesurer, pas à réussir.** Si l'écart au seuil vaut plusieurs σ,
   plus de données resserre les intervalles autour de la valeur observée : l'échec devient plus
   net, pas moins.
8. **Ventiler avant de croire une alerte agrégée.** `decode_failure_rate` a longtemps mélangé les
   deux prompts décodeurs et accusé `decode-clue` d'un décrochage de `decode-board`. La sortie de
   `score` ventile désormais `dont decode-clue` / `dont decode-board` : lire la ventilation, pas le
   total.
9. **Le registre ne se réécrit jamais.** Une conclusion invalidée se rétracte par une note ajoutée,
   qui nomme ce qu'elle corrige. C'est arrivé trois fois le 2026-08-06 ; c'est le fonctionnement
   normal, pas un incident.
10. **Aucune optimisation de prompt générateur avant P7.** Le harnais arbitre, il ne s'anticipe
    pas. Un second run générateur peut être du **matériau de contraste** pour la séance B — le dire
    explicitement dans la note, sinon il sera relu comme une tentative d'amélioration.
11. **`qwen3-8b` ne peut pas être générateur** : il est le décodeur, il re-décoderait ses propres
    indices en P6.
12. **Ne pas publier de ligne `calibré`** tant qu'un lot est marqué `AnchorSuspect` ou que la
    cohérence intra-juge est contaminée. C'est une décision de l'utilisateur, pas du harnais.

## Pièges d'artefacts

- **Empreinte de décodeur** : 12 hex de `(modèle, prompt et sa version, température, topP,
  maxOutputTokens)`. `decodesPerClue` en est **exclu** — c'est la granularité de R̄, pas le
  décodeur. Deux `recovery` d'empreintes différentes **ne se comparent pas**.
- **L'empreinte est dans le nom** : `<runId>.<empreinte>.decoded.jsonl` et
  `<runId>.<empreinte>.metrics.json` — une fratrie que `CalibrationGates.FingerprintOfMetrics`
  relit. La casser casse les portes. `DecodeFile.FindForRun` refuse **bruyamment** quand plusieurs
  décodages existent : préciser `--decoded <chemin>`.
- **La granularité est dans le nom de la calibration** : `calibration.<date>-<empreinte>-d<N>.jsonl`,
  la granularité **après** l'empreinte. Sans quoi calibrer le même décodeur à deux granularités le
  même jour vise le même chemin et `--force` écraserait des milliers de décodages déjà payés.
- **Reprises** : `decode` vérifie l'empreinte complète *et* `decodesPerClue` (deux contrôles, le
  second étant hors empreinte par construction) ; `calibrate` refuse un `--decodes` ou un
  `--epsilon` divergents — ε se décide **avant** de lire l'accord.
- **`--subset` est une correction de dénominateur**, jamais un confort : `RunMetrics.Compute`
  attribue `R̄ = 0` aux directions absentes, ce qui divise le plafond d'un run humain par quatre.
  Le drapeau restreint **tous** les dénominateurs. `--subset-outcome` s'écrit toujours en entier
  (`pass,solide,tiede` pour « toutes les issues », jamais `null`) : la porte de saturation relit
  cette provenance et refuse un fichier produit sans elle.
- **Les quatre portes en un seul verdict** : accord ≥ 0,75 · κ ≥ 0,40 · non-saturation ≤ 0,95 ·
  plancher ≤ 0,15. `calibrate` calcule les deux premières et **lit** les deux autres dans les
  `.metrics.json` désignés. Sans cette agrégation on franchit « une porte sur trois » portes sur
  quatre. Le `--saturation-metrics` doit avoir été produit avec `--subset-outcome solide`
  **exactement** : `score` écrit toujours au même chemin par run, donc un second `score` sans le
  drapeau écrase le fichier en silence et la porte serait franchie pour la mauvaise raison —
  `RequireSaturationSubset` refuse bruyamment.
- **`score --calibration` ne se replie jamais en silence** : il refuse de publier une ligne
  `calibré` si une porte est tombée ou si l'empreinte diverge, plutôt que de retomber en
  `pré-calibration`.
- **Effectifs derrière chaque taux** : sur un petit dénominateur un taux n'est qu'un plancher
  d'estimateur. Dénominateur nul ⟹ `—`, jamais un `0,000` fabriqué.
- **`strict_2of2` est un critère d'unanimité** (tous les décodages à `r = 1`), pas « les 2 mots sur
  2 » — il s'affiche `strict_2of2_all_decodes`. Même piège que `board_solved_first_try`.
- **D6 n'entre dans aucune part** (P7) : une direction sans décodage exploitable n'est pas un échec
  *sémantique*. Elle sort du numérateur, du dénominateur, de la moyenne board de `M6` et du tirage
  `--sample` ; elle est rapportée à part. Priorité de la taxonomie : **`M2 → M3 → M4 → M1 → M?`**.
- **Un seul bootstrap** : `Scoring/Bootstrap.Ci` sert le Δ`recovery`, l'accord et κ. Ne jamais en
  écrire un second.

## Toucher au code du harnais

- `SoClover.Eval` **n'est jamais déployé** — voir `CLAUDE.md` pour l'invariant Docker / `git archive`.
- Ses suites vivent dans `SoClover.Tests/Eval/`. TDD, commits atomiques, `dotnet test` complet
  après chaque tâche.
- Briques **partagées avec la prod**, à ne pas dupliquer côté éval : `Domain/BoardGeometry.cs`,
  `Domain/ClueAcceptance.cs`, `Infrastructure/AI/AiClueLlmCaller.cs`,
  `Infrastructure/AI/AiClueResponseParser.cs`, `Infrastructure/AI/LlmCallExceptions.cs`.
  `AiClueLlmCaller` **ne journalise pas** : il rend latence / version de prompt / modèle effectif /
  usage.
- **Trois compteurs de couples, trois noms** : `AgreementReport.CoupleCount` (ancres exclues),
  `FamilyAgreement.ScorableCoupleCount` (non-scorables exclus),
  `CalibrationManifest.CoupleAndAnchorCount` (ancres incluses).

## Clôture d'une séance

1. Ligne de registre écrite (`--ledger`), avec les `--notes` qui portent ce qu'aucun champ ne
   capture : thinking, contexte, réglages UI non transmis, concurrence éventuelle.
2. Note de synthèse si la séance change la lecture d'une série — en **ajoutant**, jamais en
   réécrivant.
3. `dotnet test` vert, arbre propre, commits atomiques.
4. Dire à l'utilisateur ce qui reste **indécidable**, pas seulement ce qui est mesuré.
