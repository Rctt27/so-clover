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
