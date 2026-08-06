# Registre d'expériences — harnais d'évaluation des indices IA

> Une ligne par run, **jamais réécrite**. Une correction s'ajoute, elle ne remplace pas.
>
> Statut `pré-calibration` : le décodeur n'a franchi que la porte du plancher aléatoire
> (`recovery ≤ 0,15`). Les portes d'accord ≥ 75 % et κ ≥ 0,40 relèvent de la phase P6 —
> **aucune ligne pré-calibration n'est défendable** au sens du PRD.

| date | runId | banc | prompt | version | modèle | snapshot | réglages | valid_rate | first_attempt | recovery | strict_2of2_all_decodes | half_rate | board_solved_first_try | statut | décision | hypothèse | notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 2026-07-28 | 20260728-vnone-random-baseline-seed-20260727000-a45667bc | boards.dev.jsonl | — | — | random-baseline-seed-20260727000 | — | temp 0 / topP — / maxTokens — / maxRetries 0 / reasoning False | 1,000 | 1,000 | 0,130 | 0,000 | 0,225 | 0,000 | pré-calibration | neutre | plancher aleatoire — porte recovery <= 0,15 | — |
| 2026-07-28 | 20260728-vnone-random-baseline-seed-20260727000-a45667bc | boards.dev.jsonl | — | — | random-baseline-seed-20260727000 | — | temp 0 / topP — / maxTokens — / maxRetries 0 / reasoning False | 1,000 | 1,000 | 0,130 | 0,000 | 0,225 | 0,000 | pré-calibration | neutre | plancher aleatoire — porte recovery <= 0,15 (re-score : ligne 1 omettait le decodeur) | déc. : qwen/qwen3-8b (plancher aleatoire, qwen3-8b thinking OFF, sonde) |
| 2026-07-28 | 20260728-v5-google-gemma-4-12b-qat-d79a63b9 | boards.dev.jsonl | board-clues-per-direction.md | v5 | google/gemma-4-12b-qat | 2026-07-28 | temp 1 / topP 0,95 / maxTokens 4096 / maxRetries 0 / reasoning False | 0,963 | 0,963 | 0,363 | 0,013 | 0,656 | 0,000 | pré-calibration | neutre | baseline prompt FR PerDirection v5 | gén. : gemma-4-12b-qat thinking OFF, LM Studio JIT ; déc. : qwen/qwen3-8b (qwen3-8b thinking OFF, ctx 8k) |
| 2026-08-04 | 20260728-vnone-random-baseline-seed-20260727000-a45667bc | boards.dev.jsonl | — | — | random-baseline-seed-20260727000 | — | temp 0 / topP — / maxTokens — / maxRetries 0 / reasoning False | 1,000 | 1,000 | 0,128 | 0,000 | 0,213 | 0,000 | pré-calibration | neutre | re-plancher aleatoire, decodeur clue v2 | déc. : qwen/qwen3-8b |
| 2026-08-04 | 20260728-v5-google-gemma-4-12b-qat-d79a63b9 | boards.dev.jsonl | board-clues-per-direction.md | v5 | google/gemma-4-12b-qat | 2026-07-28 | temp 1 / topP 0,95 / maxTokens 4096 / maxRetries 0 / reasoning False | 0,963 | 0,963 | 0,370 | 0,026 | 0,630 | 0,000 | pré-calibration | neutre | re-baseline v5 FR PerDirection, decodeur clue v2 | gén. : gemma-4-12b-qat thinking OFF, LM Studio JIT ; déc. : qwen/qwen3-8b |
| 2026-08-04 | 20260804-v5-google-gemma-4-12b-qat-fbb92760 | boards.dev.jsonl | board-clues-per-direction.md | v5 | google/gemma-4-12b-qat | 2026-08-04 | temp 0.7 / topP 0,95 / maxTokens 4096 / maxRetries 0 / reasoning False | 0,969 | 0,969 | 0,383 | 0,026 | 0,658 | 0,000 | pré-calibration | neutre | 2e run generateur pour seance B — v5 a temp 0,7 (contraste, pas optimisation) | gén. : gemma-4-12b-qat thinking OFF ; temp 0,7 via GENERATOR__DEFAULTTEMPERATURE ; 2e run generateur, materiau de contraste pour seance B ; déc. : qwen/qwen3-8b |
| 2026-08-04 | human-20260804-e59651fc | boards.dev.jsonl | — | — | human | — | temp 0 / topP — / maxTokens — / maxRetries 0 / reasoning False / subset=elicitation.dev.jsonl (40/160) | 0,975 | 0,975 | 0,333 | 0,000 | 0,538 | 0,000 | pré-calibration | neutre | plafond humain joue (toutes issues, pass compris) — seance A du 2026-08-04 | gén. : séance A, 40 direction(s), seed 20260804002 ; déc. : qwen/qwen3-8b |
| 2026-08-04 | human-20260804-e59651fc | boards.dev.jsonl | — | — | human | — | temp 0 / topP — / maxTokens — / maxRetries 0 / reasoning False / subset=elicitation.dev.jsonl (22/160) | 1,000 | 1,000 | 0,341 | 0,000 | 0,455 | 0,000 | pré-calibration | neutre | plafond humain sur paires resolues (solide seuls, 22/160) — A-3 | gén. : séance A, 40 direction(s), seed 20260804002 ; déc. : qwen/qwen3-8b |

## Calibrations (P6)

> Une ligne par calibration, **jamais réécrite** — même règle que la table des runs. Une
> calibration porte sur un **décodeur**, pas sur un run : c'est l'instrument qu'on valide, et
> l'empreinte en est l'identité. Tant qu'aucune ligne ne porte `franchi`, **toute ligne de run
> reste `pré-calibration`** et aucun `recovery` n'est défendable en valeur absolue.

| date | empreinte | décodeur | lot | couples | ε | accord (IC 95 %) | κ (IC 95 %) | non-sat. | plancher | verdict | notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 2026-08-06 | `9a829dc206d2` | qwen/qwen3-8b · clue v2 · temp 0,3 · maxOut 512 | comparisons.dev.jsonl | 85 + 5 ancres | 0 | **0,583** [0,438 ; 0,708] ✗ | **0,171** [-0,106 ; 0,429] ✗ | 0,341 ✓ | 0,128 ✓ | **renvoyé en P3** | 5 décodages/indice. Lot marqué `AnchorSuspect` (juge 3/5 : 2 égalités, l'aléatoire n'a jamais gagné) ; cohérence intra-juge 0,900, donc porte d'accord *atteignable*. Décodeur 5/5 sur les ancres : discrimination grossière intacte, discrimination fine nulle — κ humainVsModèle 0,026 (n=25) contre modèleVsModèle 0,233 (n=23). L'IC entier de l'accord est sous le seuil : l'échec ne s'explique pas par le bruit du lot. thinking OFF, ctx 8k. |
| 2026-08-06 | `cc992cfe4dd6` | mistralai/ministral-3-3b · clue v2 · temp 0,3 · maxOut 512 | comparisons.dev.jsonl | 85 + 5 ancres | 0 | **0,490** [0,347 ; 0,633] ✗ | **-0,017** [-0,292 ; 0,255] ✗ | 0,436 ✓ | 0,139 ✓ | **renvoyé en P3** | 5 décodages/indice, 49/85 couples tranchés. P3 première variable : **le modèle seul** change (prompt, température, topP, maxOut, décodages identiques à `9a829dc206d2`). Hypothèse testée : un modèle d'origine francophone manipulerait mieux le français. **Non confirmée à 3B** — mais les IC recouvrent ceux de qwen3-8b, donc « pire que qwen » n'est *pas* établi ; ce qui l'est, c'est que κ est indiscernable de zéro. Ancres décodeur 4/5 (contre 5/5) : la discrimination grossière commence elle-même à s'éroder. Contraste notable : la **dynamique brute est meilleure** (saturation − plancher = 0,297 contre 0,213) alors que l'ordonnancement est plus mauvais — mieux récupérer n'est pas mieux classer. Latence médiane 139 ms contre ~2,5 s : itérer sur ce modèle coûte deux ordres de grandeur de moins. Lot toujours marqué `AnchorSuspect` (juge 3/5, inchangé — même corpus). ctx par défaut LM Studio, pas de thinking. |
| 2026-08-06 | `661856eb18c6` | mistralai/ministral-3-14b-reasoning · clue v2 · temp 0,3 · maxOut 512 | comparisons.dev.jsonl | 85 + 5 ancres | 0 | **0,667** [0,524 ; 0,810] ✗ | **0,332** [0,041 ; 0,616] ✗ | 0,587 ✓ | 0,145 ✓ | **renvoyé en P3** (échec *non démontré*) | 5 décodages/indice, 42/85 couples tranchés. Troisième point de la série modèle, toujours **une seule variable** (Q4_K_M, ctx 4096). Deux seuils changent de nature ici : (1) **l'IC de κ exclut zéro** (borne basse 0,041) — les deux décodeurs précédents étaient indiscernables du hasard, celui-ci ne l'est plus ; (2) **l'IC de l'accord recouvre le seuil** (0,810 > 0,75), alors qu'il était entièrement sous la barre pour `9a829dc206d2` et `cc992cfe4dd6`. L'échec n'est donc **pas démontré**, il est indécidable au bruit actuel — statut distinct des deux lignes précédentes, à ne pas agréger avec elles. κ humainVsModèle **0,444** (n=18), au-dessus du seuil, contre 0,026 (qwen) et -0,038 (3B) : c'est la famille la plus exigeante qui progresse le plus. Contingence équilibrée (15/7/7/13), donc pas de paradoxe κ. Dynamique saturation − plancher = **0,442** (contre 0,297 et 0,213). Facteur limitant désormais **le dénominateur** : 42 couples tranchés, égalités décodeur 0,333. Modèle « reasoning » mais `reasoning_tokens = 0` mesuré sur 10 appels (17-19 tokens de complétion, `finish_reason: stop`) — le prompt clue v2 inhibe le mode par sa consigne « sans réflexion écrite ». `decode_failure_rate` global 0,085 au-dessus du garde-fou 0,05, mais la ventilation disculpe le prompt clue : **N2 18/480 tous `outOfVocabulary`, zéro `unparseable`** ; l'excès vient de N3 (`decode-board` v1, 26/40). Latences de ce plancher gonflées par des sondes concurrentes (sans effet sur les mots choisis). |

| 2026-08-06 | `27e36fefe975` | mistralai/ministral-3-14b-reasoning · clue v2 · temp 0,3 · **topP 1,0** · maxOut 512 | comparisons.dev.jsonl | 85 + 5 ancres | 0 | **0,521** [0,375 ; 0,667] ✗ | **0,038** [-0,250 ; 0,323] ✗ | 0,561 ✓ | 0,145 ✓ | **renvoyé en P3** | **9** décodages/indice, 48/85 couples tranchés. Première ligne de la **génération 2** : `topP` passe de `null` (= réglage LM Studio, non transmis) à 1,0 explicite — l'empreinte cesse de mentir, mais elle se déplace. **Deux variables bougent à la fois** (`topP` dans l'empreinte, `decodesPerClue` hors empreinte) : les effets ne sont pas séparables sur cette seule ligne, dette assumée et à solder. Le levier de granularité **fonctionne** — égalités décodeur 0,333 → 0,259, couples tranchés 42 → 48 — mais accord et κ s'effondrent (0,667 → 0,521 ; 0,332 → 0,038). La dilution seule ne l'explique pas : 28 accords sur 42 en gen1, les 6 nouveaux couples tranchés à pile ou face donneraient ~31/48 = 0,646, or on observe 25/48. Les couples déjà tranchés ont donc changé de verdict → **`topP 1,0` dégrade le décodeur**, hypothèse dominante. Cohérent avec le haut d'échelle : `strict` 2/22 → 3/22 mais `half` 14/22 → 9/22, signature d'un décodeur plus catégorique donc plus bruité. Ni le plancher (0,145, identique) ni la non-saturation (0,587 → 0,561, un quart de direction d'écart) ne bougent : l'effet porte sur l'**ordonnancement**, pas sur l'amplitude. Q4_K_M, ctx 4096, thinking vérifié inactif (`reasoning_tokens = 0`). `decode_failure_rate` 0,094 : ventilation N2 20/480 (4,2 %, tous `outOfVocabulary`) contre N3 29/40 (72,5 %) — l'alerte accuse encore `decode-clue`, c'est `decode-board` qui décroche. |

| 2026-08-06 | `dc38ea230804` | qwen/qwen3-8b · clue v2 · temp 0,3 · **topP 1,0** · maxOut 512 | comparisons.dev.jsonl | 85 + 5 ancres | 0 | **0,620** [0,480 ; 0,760] ✗ | **0,245** [-0,027 ; 0,504] ✗ | 0,371 ✓ | 0,126 ✓ | **renvoyé en P3** | **9** décodages/indice, 50/85 couples tranchés, égalités décodeur 0,153 (contre 0,224 en gen1). Réplique gen2 de `9a829dc206d2`, mêmes réglages que `27e36fefe975`. **Va en sens inverse du 14B** : accord 0,583 → 0,620 et κ 0,171 → 0,245, quand le 14B faisait 0,667 → 0,521 et 0,332 → 0,038. Le même changement améliore l'un et dégrade l'autre → l'hypothèse « `topP 1,0` dégrade », avancée sur la seule ligne `27e36fefe975`, **n'est pas soutenue** (voir note ci-dessous). Le levier de granularité se confirme en revanche sur les deux (48 → 50 couples tranchés ici). Amplitude inchangée : plancher 0,128 → 0,126, non-saturation 0,341 → 0,371. `decode_failure_rate` 0,023, **sous le garde-fou** — qwen tient `decode-board` là où les deux Mistral décrochent (N3 : 12/520 tous niveaux confondus). Q4_K_M, ctx **4096** (contre 8k en gen1 — sans effet attendu, le prompt fait ~385 tokens, mais désormais consigné par la sonde runtime). Thinking vérifié inactif (`reasoning_tokens = 0`). |

| 2026-08-06 | `6a97f056ae51` | mistralai/ministral-3-3b · clue v2 · temp 0,3 · **topP 1,0** · maxOut 512 | comparisons.dev.jsonl | 85 + 5 ancres | 0 | **0,569** [0,431 ; 0,706] ✗ | **0,149** [-0,118 ; 0,409] ✗ | 0,439 ✓ | 0,148 ✓ | **renvoyé en P3** | **9** décodages/indice, 51/85 couples tranchés, égalités décodeur 0,247. Réplique gen2 de `cc992cfe4dd6`. **Deuxième amélioration sur trois** : accord 0,490 → 0,569, κ -0,017 → 0,149. Avec qwen (+0,037) contre le 14B (-0,146), l'hypothèse « `topP 1,0` dégrade » est **abandonnée** — voir la note de synthèse. Plancher 0,148, la marge la plus fine des six calibrations (seuil 0,15) ; `decode_failure_rate` 0,044, sous le garde-fou. Q4_K_M, ctx 4096, thinking inactif (`reasoning_tokens = 0`). |

| 2026-08-06 | `27e36fefe975` | mistralai/ministral-3-14b-reasoning · clue v2 · temp 0,3 · **topP 1,0** · maxOut 512 | comparisons.dev.jsonl | 85 + 5 ancres | 0 | **0,581** [0,442 ; 0,721] ✗ | **0,164** [-0,129 ; 0,445] ✗ | 0,561 ✓ | 0,145 ✓ | **renvoyé en P3** | **5** décodages/indice, 43/85 couples tranchés. Artefact `…-d5`, à côté du `…-d9` du **même décodeur** — la granularité est désormais dans le nom (correctif `a27bc98`), sans quoi cette ligne était impossible à produire sans écraser la précédente. **Solde la dette de `27e36fefe975`** : à granularité constante (5), `topP 1,0` coûte −0,086 au 14B (0,667 → 0,581) ; à `topP` constant, 5 → 9 décodages coûte −0,060 (0,581 → 0,521). Les deux moitiés de l'écart total. Mais les mêmes changements **améliorent** qwen et le 3B : aucun des deux n'a d'effet systématique. Voir la note de synthèse mise à jour. |

| 2026-08-06 | `3f40887c807d` | qwen/qwen3-8b · **clue v3** · temp 0,3 · topP 1,0 · maxOut 512 | comparisons.dev.jsonl | 85 + 5 ancres | 0 | **0,608** [0,471 ; 0,745] ✗ | **0,234** [-0,025 ; 0,480] ✗ | 0,364 ✓ | 0,135 ✓ | **renvoyé en P3** | **9** décodages/indice, 51/85 couples tranchés, égalités décodeur 0,176. **Première variation du prompt `decode-clue`** — la seule variable que les sept calibrations précédentes partageaient. Une seule variable bouge : modèle, température, `topP`, `maxOut` et granularité sont identiques à `dc38ea230804`. v3 remplace le critère **marginal** de v2 (« les deux mots dont le lien avec l'indice est le plus fort ») par un critère **joint** (« la paire pour laquelle cet indice a été écrit » ; un mot fort accompagné d'un mot faible y est déclaré mauvaise réponse). Motivation — diagnostic agrégé sous v2 : 62,6 % des décodages à `r = 0,5` contre 6,1 % à `r = 1`, et 49 des 180 indices à R̄ exactement 0,5. **Le prompt n'a rien déplacé** : accord 0,620 → 0,608, κ 0,245 → 0,234, des écarts d'un ordre de grandeur sous le bruit d'échantillonnage. Voir la note de synthèse ci-dessous. Ancres décodeur 5/5 (discrimination grossière intacte), lot toujours `AnchorSuspect` (juge 3/5, même corpus). Gain réel mais hors portes : la robustesse de format de `decode-clue` passe à **0,006** d'échecs sur le plancher (3/480) et **0,000** sur l'humain (0/117), contre ~4,2 % pour plusieurs empreintes v2. Plancher et saturation re-mesurés sous v3 (0,135 et 0,364) — la marge du plancher se resserre, 0,126 → 0,135 pour un seuil à 0,15. Q4_K_M, ctx 4096, thinking vérifié inactif (`reasoning_tokens = 0`). |

### Note — `decode-clue` v3 change 37,5 % des réponses sans rien changer à la qualité

L'ordre de présentation des seize mots est déterministe (`ShuffleSeed.ForClue`), donc les 1620
décodages de `dc38ea230804` et de `3f40887c807d` s'apparient **un à un** : même board, même
direction, même indice, même ordre de présentation. C'est ce qui rend la comparaison ci-dessous
possible.

| | v2 `dc38ea230804` | v3 `3f40887c807d` |
|---|---|---|
| `r = 0` | 0,314 | 0,315 |
| `r = 0,5` | 0,626 | 0,625 |
| `r = 1` | 0,061 | 0,059 |
| indices à R̄ = 0,5 exact | 49 / 180 | 49 / 180 |
| accord | 0,620 | 0,608 |
| κ | 0,245 | 0,234 |

Et pourtant : **le décodeur change de paire sur 608 des 1620 décodages appariés — 37,5 %.**

v3 mord donc réellement ; ce n'est pas un prompt ignoré. Mais son effet est **orthogonal à la
qualité** : il redistribue les réponses sans déplacer d'un millième la distribution de `r`. Même
signature que les deux runs gemma du 2026-08-04, `NEUTRE` entre eux et pourtant divergents sur
62,4 % des directions.

Ce que cela établit : **le pic à `r = 0,5` n'est pas un artefact de formulation du critère.**
L'hypothèse « v2 demande la mauvaise chose — les deux meilleurs mots au lieu de la meilleure
paire » est réfutée. On a demandé explicitement la paire, avec interdiction de compléter au jugé,
et le décodeur retrouve exactement aussi souvent un seul mot sur deux. Retrouver la seconde moitié
d'une paire parmi seize mots à partir d'un indice unique est un **plafond de la tâche** pour ce
décodeur, pas un défaut d'énoncé.

Ce que cela ne réfute pas : la piste du prompt n'est pas épuisée, elle a perdu sa variante la moins
chère. Ce qui y reste est d'une autre nature — v2 comme v3 interdisent toute **délibération**
(« sans justification, sans réflexion écrite », consigne qui inhibe aussi le reasoning natif :
`reasoning_tokens = 0` mesuré ici encore). Changer l'énoncé de l'objectif ne change rien ; changer
le **calcul disponible** pour l'atteindre reste non testé. C'est une v4, et c'est une variable
distincte — ne pas l'agréger à celle-ci.

**Conséquence sur la lecture d'ensemble.** La note « les sept calibrations sont compatibles avec un
accord unique ≈ 0,58 » désignait le prompt comme piste 1 et le plafond humain inter-juges comme
piste 2. La huitième calibration tombe dans le même intervalle (0,608 ; moyenne des huit **0,580**)
et retire à la piste 1 son argument le plus direct. **La piste 2 devient la seule qui puisse encore
expliquer la série** — et elle reste non mesurée. Le seuil ne se rediscute toujours pas avant
cette mesure.

### Note — rectification : v3 ne gagne rien en robustesse de format, il en perd un peu

La ligne `3f40887c807d` affirme que la robustesse de format « passe à **0,006** d'échecs sur le
plancher et **0,000** sur l'humain, contre ~4,2 % pour plusieurs empreintes v2 ». **La comparaison
est trompeuse** : ces 4,2 % sont ceux des empreintes *ministral* (`661856eb18c6`, `27e36fefe975`),
pas ceux de qwen. À modèle constant — la garde de la variable unique vaut pour la conformité comme
pour le reste :

| échecs `decode-clue` (N2) | v2 `dc38ea230804` | v3 `3f40887c807d` |
|---|---|---|
| plancher aléatoire | **0 / 480** | 3 / 480 |
| pseudo-run humain | 0 / 117 | 0 / 117 |
| lot de calibration | **17 / 1620** (1,0 %) | **32 / 1620** (2,0 %) |
| dont `outOfVocabulary` | 15 | 29 |

v3 **dégrade** donc légèrement la conformité au lieu de l'améliorer : deux fois plus de mots hors
liste sur le lot de calibration. L'écart reste petit, très en dessous du garde-fou de 0,05, et ne
change aucun verdict ni aucune porte — mais le gain revendiqué n'existe pas, et la ligne qui le
revendique reste au registre : c'est cette note qui la corrige.

Hypothèse plausible, non testée : v3 est plus long (632 tokens de prompt contre ~385) et demande
explicitement de balayer les seize mots, ce qui expose davantage le modèle à recopier une variante
d'un mot plutôt que le mot exact.

### Note — les sept calibrations sont compatibles avec un accord unique ≈ 0,58

La septième ligne solde la dette et permet le calcul d'ensemble. Sur les sept calibrations
(3 modèles × 2 `topP` × 2 granularités, corpus identique) :

| | valeur |
|---|---|
| accord moyen | **0,576** |
| écart-type **observé** entre les sept | **0,059** |
| écart-type **attendu** si toutes mesuraient la même chose (binomial, n̄ = 47) | **0,072** |

**La dispersion observée est inférieure à celle du pur hasard.** Il ne reste donc aucune variance
à attribuer au modèle, au `topP` ou à la granularité : les sept mesures sont entièrement
compatibles avec l'hypothèse d'un accord vrai unique, autour de 0,58, pour tous les décodeurs
essayés. Ce n'est pas « on ne sait pas les départager » — c'est « il n'y a rien à départager ».

**Conséquence contre-intuitive, à retenir avant d'investir dans une séance humaine plus longue :
agrandir le corpus ne fera pas franchir la porte.** L'écart au seuil est de 0,174, soit ~2,4
écarts-types d'échantillonnage. Plus de couples resserrera les intervalles **autour de 0,58** :
l'échec deviendra plus net, pas moins. Un corpus plus grand sert à mesurer, pas à réussir.

Ce que cela laisse comme pistes, par ordre de ce qu'elles engagent :

1. **Le prompt `decode-clue`** — seule variable jamais testée, et la seule que les sept
   calibrations partagent. Mais il lui faudrait produire +0,17, bien au-delà de tout ce que la
   série a fait bouger.
2. **Le plafond humain sur la tâche de jugement, jamais mesuré.** La porte à 0,75 suppose que deux
   juges humains s'accorderaient au moins autant sur ces mêmes couples. Rien ne l'établit :
   `intra-juge = 0,900` mesure la cohérence d'**un** juge avec lui-même (et se trouve contaminée,
   cf. note du 2026-08-06), pas l'accord **inter-juges**. Si deux humains ne s'accordent qu'à 0,70
   sur « lequel de ces deux indices mène le plus directement à la paire », alors 0,75 est
   structurellement inatteignable pour n'importe quel décodeur, et c'est le **seuil** qui est mal
   posé — pas l'instrument.

> **Le seuil ne se baisse pas parce qu'il gêne.** La piste 2 n'est légitime que si l'accord
> inter-juges est mesuré **avant** de rediscuter la porte, et sur le même corpus. Le décider après
> avoir vu sept échecs serait la faute exacte qu'on a refusée sur les ancres le 2026-08-06.

### Note — synthèse des six calibrations : les trois décodeurs sont indiscernables

Les six lignes ci-dessus couvrent trois modèles × deux générations. Le classement **s'inverse
exactement** d'une génération à l'autre :

| rang | gen1 (topP implicite, 5 décodages) | gen2 (topP 1,0, 9 décodages) |
|---|---|---|
| 1 | ministral-14b **0,667** | qwen3-8b **0,620** |
| 2 | qwen3-8b 0,583 | ministral-3-3b 0,569 |
| 3 | ministral-3-3b 0,490 | ministral-14b 0,521 |

Les six intervalles de confiance se recouvrent tous ; ils partagent la plage [0,524 ; 0,633].
**Aucun des trois décodeurs ne se distingue statistiquement des deux autres.**

Ce que cela corrige, explicitement :

1. **La note « le troisième point infirme en partie »** (ci-dessous) concluait que « le modèle
   compte, et il compte beaucoup », sur la foi du seul `661856eb18c6`. Faux : ce 0,667 était une
   fluctuation haute, et le même modèle rend 0,521 sous l'autre granularité. La note qu'elle
   corrigeait — « le facteur limitant n'est probablement pas le choix du modèle », écrite sur deux
   points — avait raison.
2. **L'hypothèse « `topP 1,0` dégrade »** de la ligne `27e36fefe975` : le même changement améliore
   qwen (+0,037) et le 3B (+0,079), et ne dégrade que le 14B (-0,146). Abandonnée.

Ce qui résiste aux six lignes :

- **Aucun décodeur ne franchit les portes.** Accord de 0,490 à 0,667 pour un seuil à 0,75 ; κ de
  -0,017 à 0,332 pour un seuil à 0,40. C'est le seul constat que la série établit fermement.
- **`decodesPerClue = 9` fait ce qu'on lui demande**, sur les trois modèles sans exception :
  couples tranchés 42 → 48, 48 → 50, 49 → 51. Seul changement dont la direction soit constante.
- **`topP` ne touche pas l'amplitude** : plancher 0,128 → 0,126, 0,139 → 0,148, 0,145 → 0,145 ;
  non-saturation 0,341 → 0,371, 0,436 → 0,439, 0,587 → 0,561.

**Le facteur limitant n'est plus le décodeur, c'est le corpus.** 85 couples dont ~50 tranchés ne
suffisent pas à départager des instruments aussi proches : à ce dénominateur, l'écart-type de
l'accord avoisine 0,07, donc ±0,14 à deux sigmas — soit exactement l'amplitude de tout ce qu'on a
observé aujourd'hui. Continuer à comparer des modèles sur ce lot revient à courir après du bruit.

Deux voies devant, et le **prompt `decode-clue` reste la variable jamais testée** — la seule que
les six calibrations partagent.

### Note — deux décodeurs indépendants, un même mode d'échec

`9a829dc206d2` (Qwen, 8B, chinois) et `cc992cfe4dd6` (Ministral, 3B, français) n'ont en commun
ni la famille, ni la taille, ni la langue d'origine. Tous deux franchissent les portes d'échelle
— plancher et non-saturation — et tous deux s'effondrent sur l'ordonnancement fin : accord 0,583
et 0,490 pour un seuil à 0,75, κ 0,171 et −0,017 pour un seuil à 0,40.

Ce que ce parallèle autorise à dire : le facteur limitant n'est probablement **pas le choix du
modèle**. Deux tirages ne font pas une loi, mais ils suffisent à déplacer le soupçon vers ce que
les deux partagent — le prompt `decode-clue` v2, et la façon dont un verdict de préférence est
dérivé de R̄ (un tiers des couples reste non tranché par le décodeur ou par le juge : 49 couples
exploitables sur 85).

Ce que ce parallèle **n'autorise pas** : conclure qu'un modèle francophone plus gros échouerait
aussi. L'hypothèse de la langue d'origine n'est pas réfutée, elle est non testée à taille utile —
`ministral-3-3b` échoue peut-être simplement par manque de capacité.

### Note — le troisième point infirme en partie la note ci-dessus

Écrite sur deux décodeurs, la note précédente concluait que « le facteur limitant n'est
probablement pas le choix du modèle ». `661856eb18c6` la contredit : à prompt, température et
réglages **identiques**, accord 0,583 → 0,490 → 0,667 et κ 0,171 → −0,017 → 0,332. Le modèle
compte, et il compte beaucoup.

Ce qui reste vrai de la note : aucun des trois ne franchit les portes, et le prompt `decode-clue`
n'a toujours pas été mis en cause — il demeure la variable jamais testée. Ce qui devient faux :
l'idée que le modèle serait un levier épuisé. Il ne l'est pas.

Ce que les trois points ne permettent toujours pas de trancher : **taille et origine restent
confondues**. 14B francophone bat 8B sinophone et 3B francophone, mais aucun modèle non
francophone de 14B n'a été décodé. La monotonie observée s'explique aussi bien par la seule
capacité.

Le facteur limitant a changé de nature. Ce n'est plus la performance du décodeur mais le
**dénominateur** : 42 couples tranchés sur 85, dont un tiers perdu en égalités du décodeur.
D'où deux leviers avant tout nouveau modèle — `decodesPerClue` (hors empreinte, donc **le même
décodeur** reste jugé, à résolution plus fine) et un lot de comparaisons plus fourni.

### Note — séance B du 2026-08-06, cohérence intra-juge contaminée

Les dix doublons inversés occupaient les **dix derniers items** du lot : `ComparisonPlan` les
concaténait en queue et `SpaceOut` ne les redistribuait pas. Le juge les a repérés en cours de
séance et l'a signalé. La valeur `cohérence intra-juge = 0,900` de la ligne ci-dessus est donc
**un plafond, pas une mesure** — un juge qui sait qu'on le teste sur la cohérence n'est plus
mesuré sur elle.

Portée de la réserve : **la cohérence intra-juge seule**. Les couples de contrôle sont exclus du
calcul principal (85 couples + 5 ancres, doublons hors lot), donc ni l'accord ni κ ne s'en
trouvent affectés — ce sont eux qui portent les deux portes tombées.

Effet sur la lecture : la cohérence servait à établir que la porte d'accord était *atteignable*
(0,900 > 0,75). Cette conclusion s'appuie sur une valeur surestimée. Elle reste défendable —
l'IC 95 % entier de l'accord (0,438 ; 0,708) est sous le seuil, donc l'échec ne s'explique pas
par un juge bruité — mais elle demandera une confirmation à la prochaine séance, dont les
doublons sont désormais entrelacés (correctif `a8e74b3`).
| 2026-08-06 | human-20260804-e59651fc | boards.dev.jsonl | — | — | human | — | temp 0 / topP — / maxTokens — / maxRetries 0 / reasoning False / subset=elicitation.dev.jsonl (40/160) | 0,975 | 0,975 | 0,346 | 0,026 | 0,538 | 0,000 | pré-calibration | neutre | — | gén. : séance A, 40 direction(s), seed 20260804002 ; déc. : qwen/qwen3-8b (decode-clue v3 (critere joint), qwen3-8b Q4_K_M ctx 4096, thinking OFF verifie (reasoning_tokens=0)) |
| 2026-08-06 | 20260728-vnone-random-baseline-seed-20260727000-a45667bc | boards.dev.jsonl | — | — | random-baseline-seed-20260727000 | — | temp 0 / topP — / maxTokens — / maxRetries 0 / reasoning False | 1,000 | 1,000 | 0,135 | 0,000 | 0,238 | 0,000 | pré-calibration | neutre | — | déc. : qwen/qwen3-8b (decode-clue v3 (critere joint), qwen3-8b Q4_K_M ctx 4096, thinking OFF verifie (reasoning_tokens=0)) |
