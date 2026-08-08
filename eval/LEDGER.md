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

| 2026-08-07 | `f5bad93aeed3` | mistralai/ministral-3-14b-reasoning · **clue v4** · temp 0,3 · topP 1,0 · maxOut 512 | comparisons.dev.jsonl | 85 + 5 ancres | 0 | **0,490** [0,347 ; 0,633] ✗ | **-0,027** [-0,304 ; 0,251] ✗ | 0,576 ✓ | 0,125 ✓ | **renvoyé en P3** (échec *démontré*) | **9** décodages/indice, 49/85 couples tranchés, égalités décodeur 0,235. Calibration annoncée par la note du 2026-08-06 au soir, tenue le lendemain. **Une seule variable contre `27e36fefe975`-d9** : modèle, température, `topP`, `maxOut`, granularité et corpus identiques — seul le prompt passe de v2 à v4. L'IC entier de l'accord est sous le seuil : échec **démontré**, à ne pas agréger avec `661856eb18c6`. κ humainVsModèle 0,110 (n=18), modèleVsModèle 0,004 (n=31) — contingence équilibrée (14/13/12/10), pas de paradoxe κ. Ancres décodeur **5/5** : discrimination grossière intacte, discrimination fine nulle, même signature que les huit précédentes. Lot toujours `AnchorSuspect` (juge 3/5, même corpus) et cohérence intra-juge 0,900 toujours contaminée (doublons en queue, cf. note du 2026-08-06) — aucun statut `calibré` n'est demandé ici, le verdict est un échec. Portes d'échelle re-mesurées sous cette empreinte : plancher 0,125 (valeur théorique exacte du hasard) et non-saturation 0,576. `decode_failure_rate` des ancrages : clue **0/66** sur l'humain, **7/480 (1,5 %)** sur le plancher, tous `outOfVocabulary` ; `decode-board` décroche à 26/40 comme sous toutes les empreintes ministral — sans effet sur les quatre portes, l'outil le ventile. Q4_K_M, ctx 4096, `reasoning_tokens = 0` vérifié — **inhibé par le prompt, pas par un toggle** (voir rectification ci-dessous). Latence médiane du lot **1 018 ms**. |

### RÉSULTAT — séance D : l'humain et le décodeur devinent **exactement** aussi bien

Séance tenue le 2026-08-07, 42 directions, 18 min. Aucun appel LLM générateur ; le décodeur comparé
est `f5bad93aeed3` (ministral-14b, clue v4), run baseline re-décodé sous cette empreinte à
`--decodes 3`. **Aucune ligne dans les deux tables ci-dessus** : la séance D ne mesure ni un run
générateur ni une calibration, mais l'instrument lui-même. Ses artefacts sont
`eval/human/guessing.dev.jsonl` et le décodage apparié.

| | valeur |
|---|---|
| directions appariées | 42 |
| R̄ **humain** | **0,583** |
| R̄ **décodeur** | **0,583** |
| Δ (humain − décodeur), apparié | **0,000** IC 95 % [-0,063 ; +0,063] |
| verdict pré-enregistré atteint | **instrument valide — la cible est en cause** |

**L'IC observé (±0,063) est deux fois plus étroit que la puissance annoncée (~0,15).** L'appariement
a mieux fonctionné que prévu : humain et décodeur échouent sur les *mêmes* directions, ce qui
élimine la variance inter-items. La séance aurait donc détecté un écart de 0,07 — elle n'en trouve
aucun. Cela ne transforme pas pour autant l'absence de preuve en preuve d'équivalence, comme le
pré-enregistrement l'exigeait, mais l'absence de preuve est ici obtenue à une résolution deux fois
meilleure que promise.

**Le pic à `r = 0,5` est un plafond de la tâche, et la question est close.** Profils comparés :

| | `r = 0` | `r = 0,5` | `r = 1` |
|---|---|---|---|
| humain (n=42) | 0,024 | **0,786** | 0,190 |
| décodeur (n=460) | 0,124 | **0,746** | 0,130 |

Trois versions du prompt décodeur ont tenté de corriger ce pic — v2 marginal, v3 joint, v4 délibéré
— pour un total de zéro effet. On sait maintenant pourquoi : **un humain placé devant la même tâche
produit le même pic, plus prononcé encore.** Retrouver un seul mot sur deux à partir d'un indice
unique parmi seize mots n'est pas une faiblesse du décodeur, c'est le régime normal de l'exercice.
La note du 2026-08-06 l'avançait « pour ce décodeur » ; ce point l'établit sans restriction, et
ferme définitivement la piste du prompt comme moyen de déplacer R̄.

**Ce que le verdict autorise, et ce qu'il n'autorise pas.** Il établit que le décodeur devine comme
un humain devine — donc qu'il est un instrument valide pour *la tâche du jeu*. Il **n'établit pas**
que les neuf calibrations étaient mal conduites : elles mesuraient fidèlement ce qu'elles
mesuraient. Ce qu'il établit, c'est que **la porte d'accord jugeait l'instrument sur autre chose que
ce que le jeu contient** — comparer deux indices est un exercice de justesse, deviner en est un
d'efficacité, et le décodeur n'a jamais échoué qu'au premier. La refondation de la porte sur la
devinette **ne se décide pas ici** : le pré-enregistrement l'exigeait, elle demandera le sien.

### Limite 6 — les paires structurellement impossibles, relevée par l'auteur après la séance

Relevée par l'auteur **après** avoir joué, donc jamais déclarée d'avance : dans une partie réelle,
les deux mots visés par un indice appartiennent **toujours à deux cartes distinctes** — une
direction est une arête entre deux cartes adjacentes. Or ni la page de la séance D, ni le prompt
`decode-clue`, ne présentent la structure en cartes : les seize mots sont une liste plate. Humain
comme décodeur peuvent donc désigner une paire que le jeu interdit.

| paires intra-carte | taux |
|---|---|
| humain (3/42) | **0,071** |
| décodeur (24/126 sur les mêmes directions) | **0,190** |
| attendu au pur hasard (24 paires sur 120) | 0,200 |

**Le décodeur produit des paires impossibles au taux exact du hasard** — il n'a aucune notion de la
contrainte, ce qui est attendu puisque rien ne la lui donne. L'humain les évite 2,7 fois mieux
(~2,2 σ) sans voir les cartes davantage : il a une intuition que la machine n'a pas.

Ce que cela coûte au décodeur, sur l'ensemble du run baseline (460 décodages exploitables) :

| | n | R̄ | dont `r = 1` |
|---|---|---|---|
| inter-cartes (possible) | 367 (79,8 %) | 0,529 | 60 |
| intra-carte (impossible) | 93 (20,2 %) | 0,403 | **0** |

**Aucun décodage intra-carte ne peut valoir 1**, par construction : un cinquième des tirages du
décodeur est condamné d'avance à ne jamais donner une réponse complète. C'est un plafond mécanique
sur `strict_2of2` et une part du pic à `r = 0,5`.

**Portée sur le verdict de la séance D : aucune, et plutôt dans le sens favorable.** Les deux
joueurs subissent la contrainte, mais le décodeur bien plus que l'humain (0,190 contre 0,071) : il
atteint le score humain **malgré** ce handicap. Le verdict n'en est pas menacé. Cette lecture est
toutefois **post-hoc** — elle n'était pas pré-enregistrée, elle ne peut donc pas être portée au
crédit du résultat, seulement empêcher qu'on l'attaque par cet angle.

**Portée sur le reste du registre : réelle et non mesurée.** Tous les `recovery` publiés — les neuf
calibrations, tous les runs — reposent sur un décodeur qui gaspille ~20 % de ses tirages. `R̄` a
donc toujours **sous-estimé** la qualité des indices, de façon vraisemblablement homogène (la
contrainte ne dépend ni du modèle ni du prompt), ce qui préserve les comparaisons *entre* lignes
mais fausse toute lecture en valeur absolue.

**Ce que cela ouvre.** Une variante `decode-clue` v5 qui présente les seize mots **groupés par
carte** et énonce la contrainte. Elle se distingue de v2, v3 et v4 sur un point décisif : sa
motivation est **structurelle — une règle du jeu que l'instrument ignorait** — et non un ajustement
lu dans les données. C'est la première piste de prompt depuis le début de la série dont on puisse
dire à l'avance *pourquoi* elle devrait mordre, et le mécanisme est chiffré : 20 % de tirages
actuellement perdus, dont 0 % peut valoir 1.

### PRÉ-ENREGISTREMENT — séance D « devineur » : le décodeur imite-t-il la bonne tâche ?

**Écrit le 2026-08-07, avant tout code et avant toute mesure** (garde 6). Rien de ce qui suit ne
peut être modifié après avoir vu un résultat ; une révision se fait par note ajoutée, qui nomme ce
qu'elle change et pourquoi.

**Ce qui motive la séance.** Neuf calibrations échouent à la porte d'accord, et les trois leviers
disponibles ont été épuisés (modèle, réglages, prompt). Le diagnostic autorisé de dix désaccords a
fait apparaître une hypothèse qu'aucune des neuf ne teste : **la porte demande peut-être au
décodeur d'imiter une tâche que le jeu ne contient pas.** En séance B le juge *compare* deux
indices — un jugement de justesse. En partie, personne ne compare : un joueur *devine*, et l'indice
est bon s'il fait tomber juste. Exemple du diagnostic (`dev-024 Right`, paire Récipient + Bateau) :
le juge retient `Coque` parce qu'il couvre les deux mots, le décodeur préfère `Navire` parce qu'il
retrouve la paire plus souvent. Si c'est la devinette qui compte, c'est le décodeur qui a raison.

**Montage, figé ici.**

| | |
|---|---|
| Directions | les 4 de chacun des **11 boards dev jamais vus** par l'auteur : dev-001, 005, 008, 011, 013, 015, 016, 017, 028, 029, 036 — soit **44**, moins les directions sans indice valide |
| Indices | run **`20260728-v5-google-gemma-4-12b-qat-d79a63b9`** (baseline officiel, temp 1,0), indices déjà générés — **aucun appel LLM générateur** |
| Décodeur comparé | empreinte **`f5bad93aeed3`** (ministral-14b, clue v4), la dernière calibrée. Le run baseline sera re-décodé sous cette empreinte à `--decodes 3` (~10 min) — les décodages existants portent `9a829dc206d2`, une autre empreinte, et **deux empreintes ne se comparent pas** |
| Ordre de présentation | `ShuffleSeed.ForClue(benchHash, boardId, 0)` — **exactement** l'ordre vu par le décodage `decodeIndex 0` |
| Tâche | 16 mots mélangés + 1 indice → l'auteur désigne 2 mots. Un seul essai. Ni mots de référence, ni explication du modèle, ni score affichés |
| Mesure | `r = |picked ∩ referenceWords| / 2 ∈ {0 ; 0,5 ; 1}`, identique au décodeur ; R̄ = moyenne sur les directions |

**Comparaison** : Δ = R̄ humain − R̄ décodeur, **apparié par direction**, IC 95 % par
`Scoring/Bootstrap.Ci` — le bootstrap déjà en place, aucun second à écrire.

**Règle d'interprétation, fixée avant la mesure :**

| Résultat | Conclusion pré-enregistrée |
|---|---|
| IC de Δ **contient 0** | Humain et décodeur devinent de façon indistinguable. Le décodeur **est** un instrument valide pour la tâche du jeu, et l'échec des neuf calibrations est imputable à la **cible** — la porte d'accord jugerait le décodeur sur une tâche que le jeu ne contient pas. La porte devrait alors être refondée sur la devinette, ce qui **ne se décidera pas dans cette note** mais demandera son propre pré-enregistrement. |
| IC **entièrement > 0** (humain meilleur) | Le décodeur est un joueur plus faible qu'un humain : il sous-estime les bons indices. L'instrument est bien en cause, les neuf échecs sont mérités, et le retour en P3 reste la bonne lecture. |
| IC **entièrement < 0** | Résultat inattendu ; le montage est réexaminé **avant** toute conclusion, aucune interprétation n'est pré-autorisée. |

**Puissance déclarée d'avance.** Sur ~44 directions et un `r` à trois valeurs, l'erreur-type de Δ
vaut ≈ 0,075 : la séance ne peut détecter qu'un écart **supérieur à ~0,15**. Un IC contenant 0 sera
donc une *absence de preuve d'écart*, jamais une preuve d'équivalence — à écrire comme tel. Pour
mémoire, le décodeur rend R̄ ≈ 0,37 sur ce run.

**Limites déclarées, décidées et assumées :**

1. **Aucune ancre.** Choix explicite de l'auteur : la séance ne porte que des indices du modèle. Une
   baisse d'attention en fin de séance ne serait donc pas détectable — la séance est courte
   (~22 min), le risque est accepté, il est consigné ici et non découvert après coup.
2. **Trois boards perdus par ma faute.** `dev-019`, `dev-020` et `dev-032` étaient vierges ; ils ont
   été exposés à l'auteur pendant le diagnostic des dix désaccords, avec quatre de leurs paires et
   les indices associés. Ils sont **exclus** du vivier. Le banc test reste intact.
3. **Asymétrie d'effort.** L'humain devine une fois par direction, le décodeur trois. On compare
   donc un tirage unique à une moyenne de trois — ce qui **avantage le décodeur en stabilité**, pas
   en niveau. À rappeler en lisant Δ.
4. **Dette lexicale non traitée, sur décision de l'auteur.** Rien ne vérifie qu'un indice est un mot
   existant : `Subterrain` et `Puncture` (2 sur 213 indices modèle, 0,9 %) passent aujourd'hui la
   validation, en éval comme en partie réelle. La règle R1 de `SubstringClueValidator` couvre bien
   l'identité avec un mot du plateau (vérifié : **0 violation sur 309 indices**) — c'est l'existence
   du mot, et elle seule, qui n'est pas contrôlée. Marginal en fréquence, non corrigé pour l'instant.

#### Amendement au pré-enregistrement, même jour, avant toute mesure

Cinquième limite, omise à la première rédaction et ajoutée avant d'écrire la moindre ligne de code
— donc avant qu'aucune donnée n'existe : **l'humain garde la mémoire des boards, le décodeur non.**
Prendre les 4 directions de chaque board expose ses 16 mots quatre fois au même joueur, qui peut
écarter les paires déjà vues. Chaque appel du décodeur est au contraire indépendant : aucune
mémoire d'une direction à l'autre.

Le biais joue **en faveur de l'humain**, dans le même sens que l'asymétrie d'effort de la limite 3
joue en faveur du décodeur. Il n'est pas supprimable sans tomber à 11 directions (une par board),
dénominateur trop faible pour la mesure. Mitigation retenue : les items sont **dispersés**
(`ComparisonPlan.SpaceOut`, séparation visée de 8 positions), de sorte que deux directions d'un même
board ne se suivent jamais et que la mémoire soit la plus froide possible.

Conséquence sur la lecture, fixée ici : si Δ ressort **en faveur de l'humain**, une part non
mesurable en revient à cet avantage de mémoire — la conclusion « le décodeur est un joueur plus
faible » devra le mentionner comme surestimation possible. Si Δ **contient 0** malgré cet avantage,
la conclusion s'en trouve au contraire renforcée.

### Note — v4 sur ministral : le prompt ne bouge ni l'accord ni R̄, il ne corrige que le format

Cette calibration était le point de reprise laissé par la note du 2026-08-06. Elle répond à la
question que cette note déclarait indécidable — « un R̄ qui bondit peut améliorer l'accord comme le
dégrader : aucune sonde ne peut trancher, seule la calibration le peut ». Elle tranche : **il ne
l'améliore pas.**

**La dette de garde 1 est soldée, et dans un sens inattendu.** La note d'hier prévenait que l'effet
du modèle et celui du prompt n'étaient pas séparables, faute d'avoir conservé le R̄ de v4/qwen. La
séparation vient d'ailleurs — de `27e36fefe975`-d9, même modèle, même granularité, mêmes réglages,
prompt v2 :

| ministral-14b, d9, topP 1,0 | v2 `27e36fefe975` | v4 `f5bad93aeed3` |
|---|---|---|
| accord | 0,521 | **0,490** |
| κ | 0,038 | **-0,027** |
| R̄ du lot | 0,486 | **0,482** |
| `r = 0` / `r = 0,5` / `r = 1` | 0,157 / 0,673 / 0,129 | 0,152 / **0,725** / 0,115 |
| échecs de format | 67 / 1620 (4,1 %) | **12 / 1620 (0,7 %)** |
| latence médiane | 418 ms | 1 018 ms |

**Le bond de R̄ attribué hier au couple (modèle, prompt) revient au modèle seul.** Ministral rendait
déjà 0,486 sous v2 ; v4 rend 0,482. L'hypothèse de la sonde — le nombre de paramètres a un effet
fort sur la récupération — est **confirmée**, et le scratchpad n'y contribue pour rien. À l'inverse,
la lecture « v4 fait bondir R̄ » qu'autorisait le tableau de la note d'hier est **fausse** : elle
comparait ministral/v4 à qwen/v3 et lisait un effet de modèle comme un effet de prompt.

**Ce que v4 fait réellement**, à modèle constant, et rien d'autre :

1. **Il divise les échecs de format par six** (4,1 % → 0,7 %). C'est le premier gain de conformité
   réel et mesuré *à modèle constant* de toute la série — contrairement à celui revendiqué pour v3,
   que la note du 2026-08-06 a dû rétracter parce qu'il comparait deux modèles. Le scratchpad borné
   force le modèle à énumérer quatre mots de la liste avant de choisir, ce qui réduit les
   `outOfVocabulary`.
2. **Il concentre encore le pic `r = 0,5`** (0,673 → 0,725), en prenant surtout sur `r = 1`
   (0,129 → 0,115). Le décodeur devient plus régulièrement moyen, moins souvent parfait.
3. **Il coûte 2,4× la latence** (418 → 1 018 ms) pour ce seul gain de format.

Accord et κ, eux, ne bougent pas : Δ accord = -0,031, soit **0,44 σ**. Sous le bruit, comme v3.

**Troisième levier de prompt, troisième échec.** Les trois versions du décodeur ont maintenant été
essayées : l'énoncé marginal (v2), l'énoncé joint (v3), la délibération bornée (v4). Aucune ne
déplace l'accord. Sur les **neuf** calibrations :

| | valeur |
|---|---|
| accord moyen | **0,570** |
| écart-type **observé** entre les neuf | **0,060** |
| écart-type **attendu** si toutes mesuraient la même chose (binomial, n̄ = 48) | **0,071** |

La dispersion observée reste inférieure à celle du pur hasard. Neuf mesures — trois modèles, deux
`topP`, deux granularités, trois prompts — demeurent **entièrement compatibles avec un accord vrai
unique ≈ 0,57**. L'écart au seuil vaut 0,180, soit ~2,5 σ.

**La piste 1 (le prompt `decode-clue`) est épuisée par l'expérience.** Elle était déjà, depuis la
note du 2026-08-06, la seule variable que toutes les calibrations partageaient ; elle a maintenant
varié trois fois sans rien produire. Il reste la **piste 2 — le plafond humain inter-juges,
toujours non mesuré** — et elle est désormais seule au sens fort : ce n'est plus « la seule qui
puisse encore expliquer la série », c'est la seule qui n'ait pas été essayée. Le seuil de 0,75 ne
se rediscute toujours pas avant cette mesure (garde 6).

Ce que cette calibration **ne dit pas** : que ministral-14b serait un mauvais décodeur. Son R̄ est
le plus haut de la série (0,482 contre 0,373 pour qwen) et sa conformité sous v4 est la meilleure
jamais mesurée. Il récupère mieux et classe aussi mal — **mieux récupérer n'est pas mieux classer**,
constat déjà posé le 2026-08-06 sur le 3B et que ce point confirme à l'autre bout de l'échelle.

### Note — rectification : le `reasoning_tokens = 0` de ministral vient du prompt, pas d'un toggle

La note de sonde du 2026-08-06 écrit : « le modèle porte "reasoning" dans son nom mais son thinking
est inactif, **toggle LM Studio OFF au chargement** ; c'est *voulu* ». **L'attribution est fausse.**
Vérifié au lancement de la présente calibration : LM Studio **n'expose aucun toggle « enable
thinking » pour ce modèle**, et interrogé sans consigne particulière (« réponds uniquement par le
mot OK »), il consomme **149 `reasoning_tokens`** — son thinking est actif par défaut et rien dans
l'interface ne le désarme.

Réinterrogé avec le **système `decode-clue` v4 réel** et un board du banc, il rend
`reasoning_tokens = 0` sur 3/3, sorties de 61 à 66 tokens, aucune troncature — puis 0 sur les 1620
décodages du lot. C'est donc la **contrainte de format du prompt** qui inhibe le mode, exactement
ce qu'avançait la ligne `661856eb18c6` (« le prompt clue v2 inhibe le mode par sa consigne ») et
que la note de sonde avait ré-attribué au matériel.

Portée : **aucune sur les mesures**. Les runs ministral du 2026-08-06 et du 2026-08-07 ont bien
tourné thinking inactif, ce que leur `reasoning_tokens = 0` établit directement — seule la *cause*
était mal nommée. Ce qui change est opérationnel : il est inutile de chercher un toggle avant de
charger ce modèle, et surtout **un futur prompt décodeur plus permissif sur la forme pourrait
réveiller le thinking sans qu'on l'ait demandé**, avec un `maxOutputTokens` de 512 qui ne le
bornerait pas. Sonder `reasoning_tokens` au premier appel de toute nouvelle version de prompt.

Note annexe, sans conséquence sur les portes : la latence médiane du lot v4 est de **1 018 ms**,
contre les **3 250 ms** annoncés par la sonde d'hier pour le même modèle sous le même prompt. La
sonde tournait vraisemblablement avec un second modèle chargé ou des appels concurrents — biais
déjà consigné pour `661856eb18c6`. La calibration a coûté 28 min, non les ~2 h estimées.

### Note — sonde v4 sur `ministral-3-14b` : R̄ bondit, le critère pré-enregistré dit non — calibration à faire

**Aucune ligne de calibration** : la sonde a tourné le 2026-08-06 au soir, la calibration
(≈ 2 h d'appels) est reportée au 2026-08-07. Cette note est le point de reprise.

**Montage.** Reprise à l'identique de la sonde v4/qwen documentée ci-dessous — prompt v4 relu
directement depuis le commit `9d70e94` (pas une retranscription), mêmes 60 indices (intersection
v2 ∩ v3, seed `20260806`), 5 décodages, temp 0,3 · topP 1,0 · maxOut 512, thinking OFF. **Une
seule variable bouge : le modèle.** Hypothèse testée : le nombre de paramètres a un effet fort sur
la qualité du décodage (8 B → 14 B).

| | r = 0 | r = 0,5 | r = 1 | R̄ |
|---|---|---|---|---|
| qwen3-8b v2, mêmes indices (n=537) | 0,371 | 0,561 | 0,069 | 0,350 |
| qwen3-8b v3, mêmes indices (n=530) | 0,343 | 0,600 | 0,057 | 0,357 |
| qwen3-8b **v4** (n=300) | — | 0,631 | — | *non conservé* |
| **ministral-3-14b v4** (n=290) | **0,152** | 0,731 | **0,117** | **0,483** |

**Le critère pré-enregistré n'est pas franchi.** Il portait sur le pic `r = 0,5` contre les 0,631
de v4/qwen, seuil 2 σ, σ calculé sur l'**indice** comme unité d'analyse (écart-type des 60 moyennes
par indice / √60 — la correction de corrélation intra-indice que la note ci-dessous avait dû
appliquer *après coup*, cette fois intégrée dès l'énoncé). Résultat : Δ **+0,100**, σ 0,075, soit
**1,33 σ**. Non établi — et de surcroît dans le sens *opposé* à l'hypothèse.

**Mais ce critère est aveugle à ce qui s'est produit.** Le pic monte parce que le bas de la
distribution s'effondre : `r = 0` passe de 0,343 à **0,152** et `r = 1` double, 0,057 → **0,117**.
Le pic `r = 0,5` est une lecture en un point d'une distribution à trois : il monte aussi bien quand
les échecs reculent que quand les réussites reculent. Hérité de la sonde v4/qwen — où il testait le
levier *délibération* — il a été réutilisé pour un levier *différent* sans être réexaminé. C'est
l'erreur de conception de cette sonde.

**Ce que dit R̄, et pourquoi ça ne décide rien.** R̄ passe de ~0,36 à **0,483**, +35 % en relatif ;
l'écart tient à **≥ 2,5 σ** même en supposant la corrélation intra-indice maximale. C'est le premier
déplacement de cette ampleur de toute la série. Il reste **post-hoc** : changer de métrique après
avoir vu les résultats est ce que la garde 6 interdit, et le verdict de cette sonde demeure
« non établi ». Deux réserves s'y ajoutent :

- **Dette de garde 1 — deux variables.** Le seul comparateur à distribution complète est qwen **v3**,
  alors que ministral tourne sous **v4** : le R̄ de v4/qwen n'a jamais été conservé, seul son pic
  l'a été. L'effet du modèle et celui du prompt ne sont donc pas séparables ici. La calibration les
  sépare de fait, puisqu'elle mesure autre chose.
- **R̄ n'est pas ce que les portes mesurent.** Le décodeur ne vise pas à maximiser le taux de
  récupération mais à **imiter le jugement humain** (accord ≥ 0,75, κ ≥ 0,40), et une porte de
  non-saturation à 0,95 sanctionne justement un décodeur trop fort. Un R̄ qui bondit peut améliorer
  l'accord comme le dégrader : **aucune sonde ne peut trancher, seule la calibration le peut.**

**Conformité et coût.** `reasoning_tokens = 0` sur **300/300** — le modèle porte « reasoning » dans
son nom mais son thinking est inactif, toggle LM Studio OFF au chargement ; c'est *voulu*, la sonde
de référence était elle aussi thinking OFF. Sortie médiane 67 tokens (max 93, plafond 512), zéro
troncature. Échecs de format **10/300 (3,3 %)**, tous `outOfVocabulary`. Scratchpad : `picked ⊆
candidats` 289/290, mais **4 candidats valides seulement 209/290 (72 %)** — ministral observe moins
bien la contrainte de cardinalité que qwen. Latence médiane **3 250 ms**, soit **3,9×** qwen3-8b
sous v3 → calibration + portes ≈ **2,0 h**.

**NEXT STEP — reprise du 2026-08-07, dans cet ordre.** Décision prise : on calibre.

1. Restaurer v4 sur disque (le fichier est en v3) :
   `git show 9d70e94:SoClover.Eval/Decoder/Prompts/fr/decode-clue.md > SoClover.Eval/Decoder/Prompts/fr/decode-clue.md`.
   Le frontmatter porte déjà `version: 4` — **garde 0 satisfaite, ne rien bumper**.
2. `evalsettings.local.json` (gitignoré) : `Decoder.defaultModel = mistralai/ministral-3-14b-reasoning`.
   temp 0,3 · topP 1,0 · maxOutputTokens 512 **inchangés** (ils entrent dans l'empreinte).
3. LM Studio : ministral-3-14b-reasoning chargé, Q4_K_M, ctx 4096, **thinking OFF**. *Ne pas
   l'activer* — ce serait une seconde variable, et le plafond de sortie à 512 ne bornerait pas le
   thinking. Vérifier `reasoning_tokens = 0` au premier appel.
4. `dotnet build -c Release`.
5. **Re-décoder les deux runs d'ancrage**, sinon `CalibrationGates.RequireSameDecoder` refuse la
   calibration (les `.metrics.json` existants portent l'empreinte qwen) — `--decodes 3`, la
   granularité qu'ils portaient sous `3f40887c807d`, avec `--force` :
   `decode --run eval/runs/human-20260804-e59651fc.jsonl --decodes 3 --force`
   `decode --run eval/runs/20260728-vnone-random-baseline-seed-20260727000-a45667bc.jsonl --decodes 3 --force`
6. `score --run eval/runs/human-20260804-e59651fc.jsonl --subset eval/human/elicitation.dev.jsonl --subset-outcome solide`
   puis `score` du plancher, `--ledger eval/LEDGER.md`.
7. `calibrate --comparisons eval/human/comparisons.dev.jsonl --decodes 9 --epsilon 0
   --saturation-metrics … --floor-metrics … --notes "ministral-3-14b-reasoning Q4_K_M ctx 4096,
   thinking OFF vérifié (reasoning_tokens=0), clue v4"`.
   **`--decodes 9` et `--epsilon 0`** reprennent exactement `3f40887c807d` : la granularité est hors
   empreinte mais dans le nom du fichier, et ε se décide avant de lire l'accord.
8. Ligne de registre via `score --calibration`, puis commit. Le lot reste `AnchorSuspect` (juge 3/5)
   et la cohérence intra-juge de la séance B est contaminée : **ne pas publier de statut `calibré`**
   sans décision explicite de l'utilisateur (garde 12).

Ce qui restera indécidable après cette calibration, quel qu'en soit le résultat : le **plafond
humain inter-juges**, toujours non mesuré, et toujours la seule explication candidate à la série.

### Note — sonde `decode-clue` v4 (scratchpad borné) : mesure arrêtée avant calibration

**Aucune ligne de calibration** : la mesure s'est arrêtée sur un critère fixé d'avance, avant
d'engager les ~3 h d'appels. Cette note tient lieu de résultat.

v4 (commit `9d70e94`) = v3 + un scratchpad **dans le JSON**, émis avant le choix : `candidats`
(4 mots du plateau) puis `lien` (une phrase de 15 mots max), puis `picked`. Motivation : v3 a
établi que changer l'*énoncé* de l'objectif ne change rien ; restait à changer le *calcul
disponible*. Le scratchpad doit vivre dans l'objet JSON — `ClueDecoder.TryParsePicked` fait
`JsonDocument.Parse` sur la réponse entière, donc une délibération en texte libre avant le JSON
serait `unparseable`.

Deux sondes hors harnais (appels directs à LM Studio ; l'ordre de présentation de `ShuffleSeed`
n'est pas répliqué, donc elles mesurent le **coût** et la **conformité**, jamais l'accord) :

| | 20 indices × 5 (n=99) | 60 indices × 5 (n=298) |
|---|---|---|
| pic `r = 0,5` sous v4 | 0,525 | **0,631** |
| pic `r = 0,5` sous v2, mêmes indices | 0,609 | 0,561 |
| Δ | −0,084 | **+0,070** |

**Le signe s'inverse entre les deux sondes.** Le premier lot suggérait que le scratchpad *disperse*
R̄ — l'effet recherché : moins d'égalités, plus de couples tranchés. Le lot élargi dit l'inverse,
il *concentre* : sur les 40 indices ajoutés, le pic vaut **0,684** contre 0,525 sur les 20
premiers. C'est précisément pourquoi la sonde a été élargie avant d'engager la calibration.

Le critère pré-enregistré (|Δ| ≥ 2 σ) tombe à **1,99 σ**, du mauvais côté au dernier chiffre.
**Mais ce critère était mal spécifié, et dans le sens permissif** : les 5 décodages d'un même
indice ne sont pas indépendants, donc le σ binomial calculé sur 298 décodages sous-estime la
variance. Au σ corrigé de la corrélation intra-indice, l'écart vaut **0,9 à 1,2 σ** selon
l'hypothèse retenue (ρ = 1 ⟹ 0,89 σ ; ρ = 0,5 ⟹ 1,15 σ). La conclusion ne change pas, elle se
durcit : **effet non établi**.

Ce qui est acquis, et qui n'est pas rien :

- **Le scratchpad borné tient son budget.** Sortie médiane **60 tokens**, maximum 89, plafond 512,
  **zéro troncature** sur 400 appels ; 2 échecs de format sur 300 (`outOfVocabulary`) ;
  `picked ⊆ candidats` dans 292/298. Un scratchpad structuré dans le JSON est un mécanisme de
  délibération **contrôlable** — contrairement au reasoning natif, que le harnais ne peut pas
  borner côté décodeur (`ChatOptions` ne transmet ni budget ni effort ; le thinking ne dépend que
  du toggle LM Studio, invisible).
- **Le coût est linéaire en tokens de sortie**, à ~13 tokens/s en génération locale : 12 tokens
  (v3) → 0,66 s ; 60 tokens (v4) → 4,8 s. Calibration + portes passeraient de 25 min à **~3 h**.
  Décharger le second modèle de LM Studio n'y change rien (4881 → 4753 ms, 2,6 %) : le goulot n'est
  pas la VRAM partagée.
- Corollaire : le cran suivant envisagé (scratchpad noté, ~150 tokens) coûterait ~7 h à
  iso-protocole. **L'échelle de délibération s'arrête ici pour des raisons de machine, pas de
  méthode.**

`decode-clue.md` revient donc à **v3**, la version calibrée ; v4 reste récupérable par
`git revert 9d70e94`.

**Ce que la série dit maintenant.** Deux leviers de prompt ont été essayés sur le décodeur —
l'énoncé du critère (v3, calibré, sans effet) et la délibération écrite (v4, sondé, sans effet
établi). Il reste le **plafond humain inter-juges, toujours non mesuré**, et il est désormais seul.

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
| 2026-08-07 | human-20260804-e59651fc | boards.dev.jsonl | — | — | human | — | temp 0 / topP — / maxTokens — / maxRetries 0 / reasoning False / subset=elicitation.dev.jsonl (22/160) | 1,000 | 1,000 | 0,576 | 0,045 | 0,773 | 0,000 | pré-calibration | neutre | ancrage porte de non-saturation pour la calibration ministral-14b sous clue v4 | gén. : séance A, 40 direction(s), seed 20260804002 ; déc. : mistralai/ministral-3-14b-reasoning (ministral-3-14b-reasoning Q4_K_M ctx 4096, thinking natif inactif verifie (reasoning_tokens=0) - inhibe par le prompt, ce modele n'expose pas de toggle LM Studio ; clue v4) |
| 2026-08-07 | 20260728-vnone-random-baseline-seed-20260727000-a45667bc | boards.dev.jsonl | — | — | random-baseline-seed-20260727000 | — | temp 0 / topP — / maxTokens — / maxRetries 0 / reasoning False | 1,000 | 1,000 | 0,125 | 0,000 | 0,213 | 0,000 | pré-calibration | neutre | ancrage porte du plancher aleatoire pour la calibration ministral-14b sous clue v4 | déc. : mistralai/ministral-3-14b-reasoning (ministral-3-14b-reasoning Q4_K_M ctx 4096, thinking natif inactif verifie (reasoning_tokens=0) - inhibe par le prompt, ce modele n'expose pas de toggle LM Studio ; clue v4) |

### PRÉ-ENREGISTREMENT — `decode-clue` v5, les paires structurellement impossibles

Écrit le 2026-08-07 **avant toute mesure**, à la suite de la « Limite 6 » relevée par l'auteur
après la séance D. Rappel du défaut : une direction est une arête entre deux cartes, donc les deux
mots visés viennent **toujours de deux cartes distinctes** — or ni la page de séance ni le prompt
`decode-clue` ne présentaient la structure en cartes. Le décodeur désignait des paires intra-carte
au **taux exact du hasard** (0,190 mesuré, 0,200 attendu) et **aucune de ces paires ne peut valoir
`r = 1`**. Un cinquième des tirages était condamné d'avance.

v5 se distingue de v2, v3 et v4 sur un point décisif, et c'est la raison pour laquelle elle est
tentée après trois échecs de prompt : sa motivation est **structurelle — une règle du jeu que
l'instrument ignorait** — et non un ajustement lu dans les données.

**Montage.** Banc `boards.dev.jsonl` (hash `416b819a41a1`), run générateur
`20260728-v5-google-gemma-4-12b-qat-d79a63b9` (154 indices valides sur 160). Décodeur
`qwen/qwen3-8b`, thinking OFF, temp 0,3 · topP 1,0 · maxOut 512, `--decodes 3`. **Deux décodages
du même run, prompt v4 puis prompt v5 — une seule variable, le prompt.** Le couple (qwen3-8b, v4)
n'existait pas sur ce run : il est produit exprès comme référence, plutôt que de comparer à travers
deux modèles. Les deux décodages s'apparient direction par direction (même graine de présentation,
dérivée par deux instances de PRNG distinctes pour que le mélange à plat de v4 reste inchangé au
bit près).

Ce que v5 change, et rien d'autre : les seize mots sont présentés **groupés en quatre cartes** et
la contrainte des deux cartes est énoncée. Le **schéma JSON est identique à v4** (`candidats`,
`lien`, `picked`). La position de la carte sur le plateau et la face que porte chaque mot restent
cachées — cartes mélangées entre elles, puis mots à l'intérieur, étiquettes ordinales neutres.
Ce n'est pas une fuite : dans une partie réelle le devineur tient les quatre cartes physiques.

**Mesure principale — `intra_card_rate`.** Part des décodages exploitables dont les deux mots
sortent d'une seule et même carte. Hasard = 24/120 = **0,200**. **Critère : v5 franchit si
`intra_card_rate` ≤ 0,05.** C'est une mesure de conformité, quasi déterministe, sur n ≈ 460
décodages : entre 0,19 et 0,05 l'écart vaut plus de 7 σ, le critère ne dépend pas du bruit. Le code
**ne rejette pas** les paires intra-carte — rejeter garantirait 0,000 et ne mesurerait plus rien.

**Mesure secondaire — Δ R̄, avec sa prédiction chiffrée d'avance.** Sous ministral/v4, la
sous-population intra-carte rendait R̄ = 0,403 (n = 93, dont **0** à `r = 1`) contre 0,529 en
inter-cartes (n = 367). Si v5 se contente de réallouer ~19 % des décodages vers le comportement
inter-cartes déjà observé, le gain attendu vaut 0,19 × (0,529 − 0,403) ≈ **+0,024**. Sur 154
directions appariées, la demi-largeur de l'IC bootstrap 95 % sera de l'ordre de ±0,03 :
**l'effet prédit est au bord de la détectabilité, et il est déclaré tel avant la mesure.** Un
Δ R̄ non significatif ne réfute donc pas v5 ; un Δ R̄ **négatif hors bruit** le réfute.

**Règle d'interprétation, fixée d'avance.** Trois issues, et une seule sera lue :

1. `intra_card_rate` ≤ 0,05 **et** Δ R̄ ≥ 0 → **v5 devient le décodeur par défaut**. L'instrument
   cesse de gaspiller un cinquième de ses tirages.
2. `intra_card_rate` ≤ 0,05 **et** Δ R̄ < 0 hors bruit → la contrainte est respectée mais elle
   coûte : **v5 est rejeté**, et il restera à expliquer pourquoi un décodeur mieux informé devine
   moins bien.
3. `intra_card_rate` > 0,05 → v5 **n'obtient pas** la conformité qu'elle demande. Le prompt ne
   suffit pas, et la question devient structurelle (schéma JSON par carte, ou contrainte imposée
   au décodage plutôt qu'énoncée).

**Ce que ce montage ne mesure pas — déclaré d'avance.**

1. **Aucune des quatre portes**, ni accord ni κ. La séance D a montré que la porte d'accord juge
   l'instrument sur autre chose que ce que le jeu contient ; sa refondation sur la devinette
   demande son propre pré-enregistrement et **n'est pas engagée ici**.
2. **La qualité des indices ne bouge pas** : même run, mêmes 154 indices. Ce qui change est
   l'instrument qui les lit. Tous les `recovery` déjà publiés restent donc mesurés par un décodeur
   qui gaspillait un cinquième de ses tirages — v5 ne les corrige pas rétroactivement, elle
   empêche seulement la suite de traîner le défaut.
3. **La page de séance humaine reste à plat.** `guess.html` présente toujours les seize mots sans
   structure. La séance D reste valide — l'humain et le décodeur y voyaient tous deux à plat — mais
   elle **cesse d'être rejouable à l'identique** contre un décodeur v5. Dette explicite, à solder
   avant toute nouvelle séance devineur.
4. **Un seul modèle, une seule langue** : qwen3-8b, FR. Rien ne sera dit des autres.

### RÉSULTAT — `decode-clue` v5, 2026-08-07 — **issue 3, et pire que prévu**

Les deux décodages du pré-enregistrement ci-dessus ont été produits le 2026-08-07 (16:38 et
16:45), 6 min 25 et 6 min 42, banc `416b819a41a1`, run `20260728-…-d79a63b9`, `qwen/qwen3-8b`
thinking OFF (`reasoning_tokens = 0` vérifié sur 3 appels avec le système v5), temp 0,3 · topP 1,0
· maxOut 512, 3 décodages par indice. Empreintes `e46ee636933a` (v4) et `964eec39fd3d` (v5).

| | v4 `e46ee636933a` | v5 `964eec39fd3d` |
|---|---|---|
| `recovery` | **0,382** | **0,345** |
| `intra_card_rate` | **0,196** (90/460) | **0,245** (113/462) |
| `half_rate` | 0,643 | 0,565 |
| `strict_2of2` | 0,032 (5/154) | 0,006 (1/154) |
| distribution `r` — 0 / 0,5 / 1 | 0,285 / 0,635 / 0,080 | **0,353** / 0,578 / 0,069 |
| R̄ des paires **inter-cartes** | **0,405** | **0,348** |
| R̄ des paires **intra-carte** | 0,367 (0 × `r = 1`) | 0,389 (0 × `r = 1`) |
| échecs de format `decode-clue` | 2/462 | 0/462 |

**Δ `recovery` apparié = −3,8 pts, IC 95 % [−6,8 ; −0,7]** — l'intervalle entier est sous zéro.
Verdict de `compare` : **ÉCARTÉ**.

**Le critère principal n'est pas franchi, et il échoue dans le sens opposé.** Il demandait
`intra_card_rate ≤ 0,05` ; on mesure **0,245**, soit **au-dessus du hasard** (0,200). Montré la
structure en cartes et averti deux fois que la paire est à cheval, le décodeur enfreint la règle
**plus souvent** que lorsqu'on ne lui disait rien. C'est l'issue 3 du pré-enregistrement, dans sa
forme la plus dure : le prompt n'obtient pas la conformité qu'il demande, il l'éloigne.

**Le critère secondaire échoue aussi, et il départage les mécanismes.** La prédiction écrite
d'avance était **+0,024** par simple réallocation ; on observe **−0,038**, hors bruit. Surtout,
la perte n'est **pas** imputable aux seules paires intra-carte : R̄ **inter-cartes** — celles où la
contrainte est respectée — tombe de **0,405 à 0,348**. v5 dégrade le décodeur *sur la tâche
elle-même*, indépendamment de la contrainte. La part de `r = 0` monte de 6,8 pts. Le décodeur n'est
pas devenu plus obéissant, il est devenu **plus bruité** — et le nombre de directions où les trois
décodages tombent tous en intra-carte s'effondre de 8 à 2, signature d'une dispersion accrue et
non d'un biais systématique vers une carte.

**Ce que la sonde établit quand même, et qui vaut la dépense.** Les deux mesures indépendantes du
taux intra-carte encadrent le hasard sur un **second modèle** : 0,196 sous v4 contre 0,200 attendu.
Et `r = 1` reste à **0 sur 203** décodages intra-carte, toutes versions confondues — l'impossibilité
structurelle relevée par l'auteur après la séance D n'est plus une déduction, elle est vérifiée sur
qwen comme sur ministral. Le défaut est réel ; c'est le remède par le prompt qui ne marche pas.

**Dette de la garde 1 — deux composantes bougent dans une seule version.** v5 change à la fois la
**présentation** (seize mots groupés en quatre cartes) et le **texte de la contrainte** (règle
énoncée, rappel dans le message utilisateur, sixième contrainte absolue). Les deux ne sont pas
séparables sur cette mesure : on ne sait pas si c'est le groupement qui induit la proximité fautive,
ou l'allongement du prompt qui dilue le critère joint. Le **verdict** ne dépend pas de cette
séparation — les deux composantes partent ensemble — mais tout diagnostic sur la *cause* reste une
hypothèse.

**Décision.** v5 est **écartée**. `decode-clue.md` repasse en **v4**, qui reste le décodeur par
défaut ; v5 est archivée au commit `fa0d312` et reproductible telle quelle. Sont **conservés** :
`ShuffleSeed.ShuffleByCard` et le placeholder `{{cardGroupedBoardWords}}` (sans quoi v5 ne serait
plus rejouable), et surtout `intra_card_rate`, qui devient un témoin permanent de `score` — c'est
lui qui a rendu ce résultat lisible, et il ne coûte rien.

**Ce qui reste indécidable.** Que la contrainte des deux cartes soit **imposée au décodage** plutôt
qu'énoncée dans le prompt n'a pas été essayé : ce serait un rejet côté code, et il garantirait
`intra_card_rate = 0` sans rien mesurer — mais il rendrait au décodeur les 24 paires qu'il gaspille,
au prix d'un décodeur qui ne devine plus tout à fait comme un humain devine. Cette voie ne se décide
pas ici. Et la troisième piste de prompt consécutive qui ne déplace rien — v3, v4 sur ministral,
v5 — dit surtout que **le prompt n'est pas le levier** de ce décodeur.

### PRÉ-ENREGISTREMENT — escalier v6 / v7, séparer la mise en page du cadrage

Écrit le 2026-08-07 **avant toute mesure**, à la demande de l'auteur, qui conteste la lecture de
la sonde v5. Son objection, et elle est fondée : la mécanique d'élimination est réelle et vaut un
cinquième du champ — le second mot se cherche parmi **12** et non 15, soit 96 paires au lieu de 120
— donc un décodeur informé devrait faire *mieux*, pas moins bien. Ce que la sonde v5 a établi est
que la dégradation est réelle (IC entier sous zéro) ; ce qu'elle n'a **pas** établi est sa cause.
La dette de garde 1 déclarée dans la note v5 est ici soldée.

**Six choses bougeaient dans v5 à la fois** : un paragraphe d'interdiction, une arithmétique
(120/24/96), un point de critère enrichi, une contrainte sur `candidats`, une sixième contrainte
absolue demandant de « vérifier et recommencer », et la présentation groupée. Prompt de **916 à
1 204 tokens** (+31 %, mesuré). Trois défauts de rédaction identifiés a posteriori : (a) le cadrage
est une **interdiction** (« ne prends jamais ») là où la mécanique du jeu est une **élimination
constructive** (« cherche parmi les douze ») ; (b) la contrainte 5 demande une opération que le
format rend impossible — vérifier et recommencer, alors qu'aucun texte n'est autorisé hors du JSON
et que `picked` s'écrit une fois ; (c) la mise en page **induit peut-être l'erreur qu'elle
interdit**, en posant les quatre mots d'une carte sur des lignes consécutives.

**Montage.** Identique à celui de la sonde v5, banc `416b819a41a1`, run `20260728-…-d79a63b9`,
`qwen/qwen3-8b` thinking OFF, temp 0,3 · topP 1,0 · maxOut 512, 3 décodages par indice. Escalier à
**une variable par marche** :

| | ce qui change | ce que ça isole |
|---|---|---|
| **v4** (mesuré, `e46ee636933a`) | — | référence : `intra_card_rate` 0,196, R̄ 0,382 |
| **v6** | présentation groupée **seule**, pas un mot de texte changé | l'effet de **mise en page** |
| **v7** | v6 **+ une seule phrase**, cadrage constructif | l'effet du **cadrage** |

La phrase de v7, et rien d'autre : « Les deux mots visés sont sur deux cartes différentes : une fois
que tu tiens le premier, cherche son partenaire parmi les douze mots des trois autres cartes. » Pas
d'arithmétique, pas de sixième contrainte, pas de « recommence », pas de consigne sur `candidats`.

**Prédiction posée d'avance, et elle discrimine.** Si la **mise en page** est en cause, `v6` monte
déjà au-dessus de 0,196 **alors qu'aucun mot ne parle de cartes** — un prompt qui ne mentionne pas
la contrainte ne peut pas la faire violer, seule la disposition le peut. Si c'est la **rédaction de
v5**, `v6` reste à 0,196 ou descend, et c'est `v7` qui doit mordre, cette fois dans le bon sens.
Les deux issues sont informatives ; c'est ce qui manquait à v5.

**Lecture, fixée d'avance.** `intra_card_rate` reste le témoin principal (hasard 0,200), Δ`recovery`
apparié le témoin secondaire, sur les trois comparaisons v4→v6, v6→v7 et v4→v7. Aucune porte de
calibration n'est engagée. Aucun statut `calibré` n'est demandé.

### RÉSULTAT — escalier v6 / v7, 2026-08-07 — **c'est la mise en page, et mon diagnostic était faux**

Décodages lancés à 17:20 (v6, 6 min 10) et 17:27 (v7, 6 min 03), même montage que la sonde v5.
Empreintes `1ddcae98c15a` (v6) et `78777a58b54e` (v7).

| | présentation | texte sur les cartes | `intra_card_rate` | `recovery` | R̄ inter-cartes | part de `r = 1` | `strict_2of2` |
|---|---|---|---|---|---|---|---|
| **v4** `e46ee636933a` | à plat | aucun | **0,196** | 0,382 | 0,405 | 0,080 | 0,032 |
| **v5** `964eec39fd3d` | groupée | lourd (6 ajouts, +31 % de prompt) | **0,245** | 0,345 | 0,348 | 0,069 | 0,006 |
| **v6** `1ddcae98c15a` | groupée | **aucun** | **0,485** | 0,370 | 0,388 | 0,046 | 0,013 |
| **v7** `78777a58b54e` | groupée | une phrase constructive | **0,453** | 0,377 | 0,425 | 0,074 | 0,026 |

Δ`recovery` appariés : v4→v6 **−1,2 pts** [−4,0 ; +1,5] `NEUTRE` · v6→v7 **+0,7 pts** [−1,0 ; +2,3]
`NEUTRE` · v4→v7 **−0,5 pts** [−3,3 ; +2,3] `NEUTRE`.

**La prédiction pré-enregistrée tranche, et elle tranche contre moi.** Elle disait : si la mise en
page est en cause, `v6` monte au-dessus de 0,196 *alors qu'aucun mot ne parle de cartes*. v6 monte
à **0,485** — deux fois et demie le hasard, deux fois le taux de v5. Grouper les seize mots en
quatre blocs suffit à faire choisir deux mots de la même carte **une fois sur deux**, sans qu'aucune
ligne du prompt n'ait mentionné l'existence des cartes. La proximité typographique est un attracteur
massif, et c'est **moi** qui l'ai introduit dans v5.

**Mon diagnostic « c'est ma rédaction » est réfuté, et exactement à l'envers.** Le texte lourd de v5
— celui que j'avais qualifié d'interdiction mal formulée, avec sa contrainte impossible et son
arithmétique — est le **plus efficace des trois** à contenir la violation : 0,485 → **0,245**, une
division par deux. La phrase légère et « bien cadrée » de v7 ne fait que 0,485 → 0,453. Plus le
texte insiste, plus il contient l'attracteur ; aucun ne le ramène au niveau de la présentation à
plat. Ce qui était présenté comme trois défauts de rédaction était en réalité trois compensations
d'un défaut de présentation.

**Le coût de v5 n'était donc pas le groupement.** v6 et v7 sont `NEUTRE` en `recovery` contre v4 ;
seule v5 perdait (−3,8 pts, IC entier sous zéro). La perte est imputable à ce qui est **propre à
v5** — +31 % de prompt, six ajouts, une consigne inexécutable — et non au fait de grouper.

**Le piège que R̄ ne voit pas, et qui compte.** v6 est `NEUTRE` en `recovery` tout en faisant
**chuter les récupérations exactes** : `r = 1` passe de 0,080 à **0,046**, `strict_2of2` de 0,032 à
0,013. R̄ tient parce que `r = 0,5` monte en compensation (0,635 → 0,675). Mécaniquement attendu :
la moitié des tirages étant intra-carte, la moitié des tirages **ne peut pas** valoir 1. Une
présentation peut donc décapiter le haut de l'échelle de l'instrument sans que la métrique
principale bronche — à lire avec `intra_card_rate` à côté, désormais.

**Ce que l'objection de l'auteur devient.** Sa mécanique est juste et n'est pas en cause : une fois
le premier mot tenu, le second se cherche parmi douze. Ce que l'escalier établit, c'est que **ce
modèle ne peut pas exploiter la partition tant qu'elle lui est présentée en blocs** — le gain
d'information (un cinquième du champ) est englouti par un biais de proximité qui vaut, lui, deux
fois et demie le hasard. Le levier n'est pas mort : il n'a **jamais été testé sans le confond**.

**Ce que cela ouvre.** Porter l'appartenance de carte **sans le bloc** — une liste à plat, mélangée
comme dans v4, où chaque mot porte son étiquette en ligne (`- Volcan (carte C)`). L'ordre de
balayage de v4 est préservé, aucune proximité n'est créée, et la partition reste lisible. C'est la
seule forme dans laquelle la mécanique d'élimination peut être évaluée pour ce qu'elle vaut. Non
engagé ici.

**Décision.** v6 et v7 sont écartées comme v5 ; `decode-clue.md` reste en **v4**. Les trois variantes
sont archivées (`fa0d312`, `cce2c7e`, `8c33bea`) et rejouables.
| 2026-08-07 | 20260728-v5-google-gemma-4-12b-qat-d79a63b9 | boards.dev.jsonl | board-clues-per-direction.md | v5 | google/gemma-4-12b-qat | 2026-07-28 | temp 1 / topP 0,95 / maxTokens 4096 / maxRetries 0 / reasoning False | 0,963 | 0,963 | 0,382 | 0,032 | 0,643 | 0,000 | pré-calibration | neutre | escalier des paires impossibles - reference clue v4 (mots a plat) sur qwen3-8b | gén. : gemma-4-12b-qat thinking OFF, LM Studio JIT ; déc. : qwen/qwen3-8b (reference v4 sur qwen3-8b pour la sonde v5 ; thinking OFF, reasoning_tokens=0 verifie) |
| 2026-08-07 | 20260728-v5-google-gemma-4-12b-qat-d79a63b9 | boards.dev.jsonl | board-clues-per-direction.md | v5 | google/gemma-4-12b-qat | 2026-07-28 | temp 1 / topP 0,95 / maxTokens 4096 / maxRetries 0 / reasoning False | 0,963 | 0,963 | 0,345 | 0,006 | 0,565 | 0,000 | pré-calibration | neutre | escalier - clue v5 (groupe par carte + contrainte enoncee) : ECARTEE, intra_card_rate 0,245 > hasard | gén. : gemma-4-12b-qat thinking OFF, LM Studio JIT ; déc. : qwen/qwen3-8b (sonde v5 (mots groupes par carte, regle des deux cartes) sur qwen3-8b ; thinking OFF, reasoning_tokens=0 verifie) |
| 2026-08-07 | 20260728-v5-google-gemma-4-12b-qat-d79a63b9 | boards.dev.jsonl | board-clues-per-direction.md | v5 | google/gemma-4-12b-qat | 2026-07-28 | temp 1 / topP 0,95 / maxTokens 4096 / maxRetries 0 / reasoning False | 0,963 | 0,963 | 0,370 | 0,013 | 0,695 | 0,000 | pré-calibration | neutre | escalier - clue v6 (groupe par carte, aucun texte) : la mise en page seule porte intra_card_rate a 0,485 | gén. : gemma-4-12b-qat thinking OFF, LM Studio JIT ; déc. : qwen/qwen3-8b (escalier v6 : presentation groupee seule, aucun texte sur les cartes ; qwen3-8b thinking OFF) |
| 2026-08-07 | 20260728-v5-google-gemma-4-12b-qat-d79a63b9 | boards.dev.jsonl | board-clues-per-direction.md | v5 | google/gemma-4-12b-qat | 2026-07-28 | temp 1 / topP 0,95 / maxTokens 4096 / maxRetries 0 / reasoning False | 0,963 | 0,963 | 0,377 | 0,026 | 0,669 | 0,000 | pré-calibration | neutre | escalier - clue v7 (groupe + une phrase de cadrage constructif) : 0,453, le texte leger ne compense pas | gén. : gemma-4-12b-qat thinking OFF, LM Studio JIT ; déc. : qwen/qwen3-8b (escalier v7 : presentation groupee + une phrase de cadrage constructif ; qwen3-8b thinking OFF) |

## Pistes ouvertes au 2026-08-07 — aucune n'est engagée

Trois pistes sortent de la journée. Elles sont écrites ici pour ne pas être re-dérivées ; **aucune
n'est décidée**, et chacune demandera son propre pré-enregistrement.

### Piste 1 — refonder la porte d'accord sur la devinette (séance E)

Discutée le 2026-08-07 et non consignée jusqu'ici. Elle découle de la séance D : la porte actuelle
demande au décodeur de **reproduire le classement d'un juge** (« lequel de ces deux indices est le
meilleur ? »), exercice que le jeu ne contient pas ; neuf calibrations donnent 0,57 pour un seuil à
0,75, avec une dispersion **inférieure** au hasard d'échantillonnage — aucun réglage ne la déplacera.

**Ce que la porte affirmerait à la place** : le décodeur est valide si son taux de récupération est
**interchangeable avec celui d'un devineur humain**. C'est la seule propriété dont on a besoin pour
que `recovery` veuille dire quelque chose.

**Montage** : reprendre **les 42 mêmes directions** de la séance D, même page, même plan
déterministe, avec une **seconde personne**. Rien à construire — le verbe `guess` et sa page
existent. On en tire trois écarts appariés : Δ(H1, H2), **jamais mesuré** ; Δ(H1, D) = 0,000
[−0,063 ; +0,063], déjà connu ; Δ(H2, D) en contrôle.

**Critère** : le décodeur passe si son écart à un humain **n'excède pas** l'écart entre deux humains
— il tombe *dans* la dispersion humaine. Critère **relatif**, donc sans nombre arbitraire : le 0,75
actuel avait été posé sans jamais mesurer ce que deux humains atteignent, ce que la garde 6
interdit précisément de contourner. Les portes de **plancher** et de **non-saturation** ne bougent
pas ; ce sont l'accord et κ qui sortent.

**Coût et limites, chiffrés.** La séance D a coûté **18 minutes** (42 devinettes, 21 s médianes).
Mais à n = 42 chaque écart porte ±0,06 : comparer deux écarts de cette précision donne une lecture
**grossière**, et une porte défendable en demandera sans doute deux ou trois personnes. Atteindre
une demi-largeur de 0,05 demanderait ~67 directions, or **29 des 40 boards de `boards.dev.jsonl`
sont brûlés** — ces 42 directions sont à peu près tout ce qui reste de vierge. Aller au-delà
signifie entamer le test set, ce qui se consigne.

### Piste 2 — porter l'appartenance de carte sans le bloc

Ouverte par l'escalier v6/v7 ci-dessus : la mécanique d'élimination décrite par l'auteur est juste
(le second mot se cherche parmi douze, pas quinze) mais elle n'a **jamais été évaluée sans le
confond** — les trois variantes groupaient les mots en blocs, et le biais de proximité qui en résulte
vaut deux fois et demie le hasard. La forme qui les sépare : liste **à plat**, mélangée exactement
comme v4, chaque mot portant son étiquette en ligne (`- Volcan (carte C)`). Ordre de balayage de v4
préservé, aucune adjacence créée, partition lisible.

### Piste 3 — rendre la paire intra-carte impossible par une boucle de retry (demandée par l'auteur)

**Demande de l'auteur, 2026-08-07, à investiguer — pas analysée ici.** L'idée : plutôt que de laisser
le décodeur produire une paire intra-carte et de la compter, lui **renvoyer une réponse du harnais**
qui l'invite à recommencer selon une boucle de retry précise, sur le modèle de **ce qui existe déjà
en production** — quand le LLM générateur produit un indice invalide, le backend lui renvoie un
retour structuré (`{{retryFeedback}}` / `{{rejectedAttemptsByDirection}}` des prompts
`board-clues*.md`) qui cadre la nouvelle tentative.

Ce qu'il s'agit d'investiguer : **peut-on intégrer un mécanisme équivalent au décodage côté Eval ?**
La brique existe et est éprouvée côté jeu ; la question est de savoir ce qu'elle donnerait ici, à
quel coût en appels, et sous quelle forme de retour.

Un point sera à trancher **au moment de l'investigation, pas avant** : le harnais ne rejette
aujourd'hui aucune paire intra-carte **par choix** — la note de la sonde v5 pose que rejeter en code
garantirait `intra_card_rate = 0,000` sans rien mesurer. Une boucle de retry n'est pas un rejet sec,
puisqu'elle rend la main au modèle ; savoir si elle change la **nature de l'instrument** (un
décodeur assisté devine-t-il encore comme un humain devine ?) fait partie de ce qu'il faudra
examiner. Aucune conclusion n'est tirée ici.

### ANALYSE À COÛT NUL — 2026-08-08 — ce que vaudrait un resampling inter-cartes

Instruit la piste 3 ci-dessus **sans payer un seul appel**. Méthode : post-stratification sur les
`.decoded.jsonl` déjà produits — pour chaque direction on ne retient que les décodages
**inter-cartes** observés, et on recalcule R̄ (moyenne par item, puis moyenne des items) et la part
de `r = 1`. Deux contrôles de validité du calcul : `intra_card_rate` est reproduit **à la troisième
décimale** sur les quatre variantes du registre, et le Δ apparié humain/décodeur de la séance D est
reproduit à **+0,000**. Banc `416b819a41a1`, run `20260728-…-d79a63b9`, 154 directions, 460
décodages par variante.

| décodeur | `intra` | R̄ obs | R̄ post-strat | Δ | `r=1` obs | `r=1` post-strat | dir. sans tirage valide |
|---|---|---|---|---|---|---|---|
| **ministral v4** `f5bad93aeed3` | 0,202 | 0,502 | 0,519 | **+0,017** | 0,130 | **0,163** | 10 |
| qwen v4 `e46ee636933a` | 0,196 | 0,397 | 0,400 | +0,003 | 0,080 | 0,100 | 8 |
| qwen v5 `964eec39fd3d` | 0,245 | 0,358 | 0,360 | +0,002 | 0,069 | 0,092 | 2 |
| qwen v6 `1ddcae98c15a` | 0,485 | 0,385 | 0,395 | +0,010 | 0,046 | 0,089 | 26 |
| qwen v7 `78777a58b54e` | 0,453 | 0,392 | 0,431 | +0,040 | 0,074 | 0,135 | 23 |
| humain, séance D | 0,071 | 0,583 | 0,590 | +0,007 | 0,190 | 0,205 | 3 |

**RECTIFICATION — la colonne « R̄ inter-cartes » du registre n'est pas le gain d'un resampling.**
Elle avait été lue comme tel (0,382 → 0,405 sur qwen v4, soit +2,3 pts annoncés). C'est faux : ce
chiffre **re-pondère les items**, une direction où le modèle pioche souvent intra-carte y pesant
moins. À composition d'items constante, le gain vaut **+0,3 pt sur qwen** et **+1,7 pt sur
ministral**. La colonne reste juste pour ce qu'elle décrit — le rendement des tirages inter-cartes —
mais ne doit plus servir à estimer un resampling.

**Le gain sur R̄ est marginal ; il est concentré sur le haut de l'échelle.** Sur le décodeur validé
(ministral), R̄ ne bouge que de +1,7 pt, mais `r = 1` passe de 0,130 à 0,163 — **+25 % en relatif**.
C'est la seule justification sérieuse d'un resampling : `recovery` sature et ne discriminera pas des
prompts générateurs, `r = 1` si.

**Le dégât de v6 n'est pas réparable par filtrage.** Malgré 48,5 % des tirages retirés, R̄ ne remonte
que de +0,010 et `r = 1` reste à 0,089, **sous** v4 (0,100). La présentation groupée n'a pas
seulement pollué le support du tirage : elle a dégradé le raisonnement lui-même. La lecture de
l'escalier v6/v7 en sort durcie, et la présentation en blocs est close.

**Renversement — le risque est l'inverse de celui qu'on redoutait.** Sur les 42 directions de la
séance D, à conditions égales :

| | `r = 0` | `r = 0,5` | `r = 1` | `intra` | R̄ |
|---|---|---|---|---|---|
| humain | 0,024 | 0,786 | **0,190** | 0,071 | 0,583 |
| ministral v4 | 0,056 | 0,722 | **0,222** | 0,190 | 0,583 |

Le décodeur touche **déjà** la cible exacte plus souvent qu'un humain, tout en violant la règle 2,7×
plus souvent. Sous resampling il monterait à ~0,274 contre ~0,205 pour l'humain corrigé. L'hypothèse
« le décodeur est handicapé par des paires impossibles » est donc à retourner : ce bruit est
peut-être ce qui le **maintient** au niveau humain. Corriger le support risque de le rendre
**sur-humain sur `r = 1`** tout en le laissant équivalent en moyenne — une perte de validité, dans
l'autre sens. Sur R̄ l'équivalence tiendrait (Δ passerait de 0,000 à ~+0,010, très en deçà de
l'IC ±0,063).

**Limites, à ne pas escamoter.** (1) L'estimateur est **optimiste** : il suppose que le tirage de
remplacement ressemblerait aux tirages inter-cartes du même item, alors que piocher intra-carte est
vraisemblablement le symptôme d'un item où le modèle est perdu — le +0,003 de qwen appuie cette
lecture. Le gain réel sera ≤ à l'estimation. (2) 10 directions (ministral) n'ont **aucun** décodage
inter-carte et sortent de l'estimation. (3) **Aucun des écarts ci-dessus n'est établi** : tous sont
sous 1,5 σ. Ce sont des ordres de grandeur pour décider s'il vaut la peine de mesurer, pas des
mesures.

**Ce que la piste 3 devient.** Le **retry avec feedback** est écarté : il rendrait la main au modèle
et détruirait la seule preuve de validité qu'on possède (séance D, décodeur en un coup) ; et v5/v7
ont déjà montré qu'énoncer la contrainte n'aide pas ce modèle. Lui est substitué le **resampling
sans feedback** — relancer le même appel inchangé jusqu'à obtenir une paire inter-cartes —, qui
échantillonne la distribution du modèle conditionnée à son support valide, ne dit rien au modèle, et
**conserve `intra_card_rate` intégralement** puisque la violation est comptée avant correction. La
note de la sonde v5 (« rejeter en code garantirait 0,000 et ne mesurerait rien ») reste vraie du
rejet **sec**, et se trouve ici **partiellement rétractée** : compter et corriger ne s'excluent pas.
Coût estimé : ×1,25 d'appels sous `intra = 0,20`.

### PRÉ-ENREGISTREMENT — marche v8, l'appartenance de carte sans le bloc

Écrit le 2026-08-08 **avant toute mesure**. Ouvre la piste 2, et elle seule : l'escalier v6/v7 a
établi que grouper les seize mots en blocs porte `intra_card_rate` à 0,485, soit deux fois et demie
le hasard, **sans qu'aucun mot du prompt ne mentionne les cartes**. La mécanique d'élimination
décrite par l'auteur — une fois le premier mot tenu, le second se cherche parmi douze et non quinze
— n'a donc **jamais été évaluée sans ce confond**.

**Montage.** Banc `416b819a41a1`, run `20260728-…-d79a63b9`, `qwen/qwen3-8b` thinking OFF, temp 0,3 ·
topP 1,0 · maxOut 512, 3 décodages par indice. **Le modèle reste qwen** : v8 prolonge l'escalier
v4/v6/v7, tous mesurés sur qwen, et la garde 1 interdit de changer le modèle dans la même série. La
validation d'instrument (resampling, séance humaine) se fera sur ministral et fait l'objet d'un
pré-enregistrement séparé.

**v8 = v4 au mot près.** Liste **à plat**, mélangée par le `ShuffleSeed` habituel (pas
`ShuffleByCard`), aucune adjacence créée, ordre de balayage de v4 préservé. Seul ajout : chaque mot
porte son étiquette de carte en ligne (`- Volcan (carte C)`). **Aucun texte du prompt ne mentionne
les cartes** — même discipline que v6, pour que l'étiquette soit la seule variable. Une éventuelle
marche v9 (v8 + phrase de cadrage) n'est pas engagée ici.

**Amendement écrit à l'implémentation, toujours avant la moindre mesure.** Une seule dérogation au
« v4 au mot près » : la contrainte 3 de v4 demande de copier les mots « à l'identique depuis la
liste », et chaque mot portant désormais son étiquette, elle inviterait littéralement à écrire
`"Volcan (carte C)"` dans `picked`. Cinq mots sont ajoutés — « sans l'étiquette entre parenthèses
qui les suit » — **purement de format** : ils ne disent rien de la partition, rien de la règle des
deux cartes, et n'invitent pas à exploiter l'information. Sans eux la marche mesurerait surtout
l'aptitude du modèle à ne pas recopier une parenthèse, ce qui n'est pas la question posée. La
dérogation est déclarée ici pour que le verdict soit lu avec elle ; `decode_failure_rate` reste à
surveiller, un décrochage signalerait que la précision n'a pas suffi.

**Critère de succès de la piste, fixé d'avance.** `intra_card_rate` témoin principal. La piste 2
n'est retenue que si v8 descend **sous** v4 d'au moins 2 σ : σ = √(0,2 × 0,8 / 460) = 0,0186, donc
succès si `intra_card_rate ≤ 0,159`. Entre 0,159 et 0,196, l'étiquette est neutre — la partition est
lisible mais inexploitée. Au-dessus de 0,233, l'étiquette est elle-même un attracteur, plus faible
que le bloc mais réel. Δ`recovery` apparié en témoin secondaire, sur v4 → v8.

**Prédiction posée d'avance, et elle est pessimiste : `intra_card_rate ≈ 0,28`, donc échec du
critère.** Raison : la leçon de v6 est que ce modèle ne sait pas *exploiter* une partition, il la
*subit* — et l'étiquette, quoique moins saillante qu'un bloc, reste une structure. Deux forces
s'opposent (information de partition à la baisse, saillance à la hausse) et rien dans la série ne
laisse croire que la première l'emporte sur ce modèle. Si v8 descend malgré tout sous 0,159, la
prédiction est réfutée et la piste 2 devient la voie principale.

**Contrôle obligatoire.** Sonder `reasoning_tokens` au premier appel : v8 change la forme du bloc de
mots, et un prompt plus permissif peut réveiller un thinking natif que `maxOutputTokens = 512` ne
bornerait pas. Bumper `version:` à 8 dans le frontmatter **dans le même geste** que l'édition du
contenu — `DecoderFingerprint` ne hache que la version déclarée.

Aucune porte de calibration n'est engagée. Aucun statut `calibré` n'est demandé.
| 2026-08-08 | 20260728-v5-google-gemma-4-12b-qat-d79a63b9 | boards.dev.jsonl | board-clues-per-direction.md | v5 | google/gemma-4-12b-qat | 2026-07-28 | temp 1 / topP 0,95 / maxTokens 4096 / maxRetries 0 / reasoning False | 0,963 | 0,963 | 0,369 | 0,039 | 0,610 | 0,000 | pré-calibration | neutre | escalier - clue v8 (a plat + etiquette de carte en ligne) : ECARTEE, intra_card_rate 0,314 contre critere 0,159 ; la prediction pre-enregistree 0,28 tient | gén. : gemma-4-12b-qat thinking OFF, LM Studio JIT ; déc. : qwen/qwen3-8b |


### RÉSULTAT — marche v8, 2026-08-08 — **l'étiquette est un attracteur intermédiaire, la piste 2 est fermée**

Décodage lancé à 18:58, terminé en **6 min 26**, empreinte `1143466d9456`. Montage conforme au
pré-enregistrement, amendement de format compris.

| | présentation | texte sur les cartes | `intra_card_rate` | `recovery` | part de `r = 1` | `strict_2of2` |
|---|---|---|---|---|---|---|
| **v4** `e46ee636933a` | à plat, nue | aucun | **0,196** | 0,382 | 0,080 | 0,032 |
| **v8** `1143466d9456` | à plat, **étiquetée** | aucun | **0,314** | 0,369 | 0,082 | 0,039 |
| **v6** `1ddcae98c15a` | groupée | aucun | **0,485** | 0,370 | 0,046 | 0,013 |
| **v7** `78777a58b54e` | groupée | une phrase | **0,453** | 0,377 | 0,074 | 0,026 |
| **v5** `964eec39fd3d` | groupée | lourd (6 ajouts) | **0,245** | 0,345 | 0,069 | 0,006 |

Δ`recovery` apparié v4 → v8 : **−1,4 pts, IC [−3,9 ; +1,0]**, `NEUTRE` — comme v6 et v7.

**Le critère pré-enregistré tombe, et la prédiction tient.** Succès demandait
`intra_card_rate ≤ 0,159` ; on mesure **0,314**, soit au-dessus du hasard et **dans la zone
« attracteur réel »** annoncée d'avance (> 0,233). La prédiction écrite avant la mesure disait
0,28 ; observé 0,314, à 1,8 σ. **C'est la première prédiction pré-enregistrée de la série qui se
vérifie** — les précédentes s'étaient trompées de signe (v5) ou de cause (v6/v7).

**La série se lit maintenant sur deux axes additifs et opposés.** v8 fournit le point qui manquait
au premier :

- **à texte constant (aucun), la saillance de la partition fait monter la violation** :
  rien 0,196 → étiquette en ligne 0,314 → bloc 0,485 ;
- **à présentation constante (bloc), le texte de contrainte la fait descendre** :
  nu 0,485 → une phrase 0,453 → texte lourd 0,245.

Aucune des cinq formes ne ramène la violation sous le niveau de v4, qui **ne dit rien du tout**. La
conclusion est nette et elle est structurelle : **ce modèle ne peut pas recevoir l'information de
partition sans être amorcé vers elle**. Plus la structure est saillante, plus il choisit deux mots
de la même carte — exactement l'inverse de ce que l'information devrait produire.

**Ce que devient l'objection de l'auteur.** Sa mécanique d'élimination — une fois le premier mot
tenu, le second se cherche parmi douze et non quinze — était juste, et l'escalier v6/v7 l'avait
laissée indécidable parce que toutes les variantes créaient une adjacence. v8 la teste **sans le
confond** : ordre de balayage de v4 préservé au mot près, aucune adjacence, partition lisible. Elle
ne se matérialise pas. Cela ne réfute pas la mécanique — elle reste vraie du jeu — mais établit que
**ce décodeur ne l'exécute pas**, quelle que soit la forme sous laquelle on la lui donne.

**Deux acquis secondaires.** (1) La précision de format a fonctionné au-delà du nécessaire :
`decode_failure_rate` côté `decode-clue` vaut **0,000** (0/462), meilleur que v4 — le modèle n'a
jamais recopié l'étiquette dans `picked`. L'amendement déclaré n'a donc pas pollué la mesure.
(2) L'invariant tient toujours : **0 décodage intra-carte à `r = 1`** sur les 145 de v8, ce qui
porte le cumul à **348 sur 348** décodages intra-carte, trois modèles et cinq prompts.

**Post-stratification, pour mémoire** : v8 gagnerait +0,001 sur R̄ et passerait de 0,082 à 0,120 sur
`r = 1` — même profil que les autres variantes, le filtrage ne répare pas ce que la présentation
a coûté.

**Décision.** v8 est écartée comme v5, v6 et v7 ; `decode-clue.md` **repasse en v4**, archivée et
rejouable. **La piste 2 est close.** Avec elle se clôt la série des prompts décodeurs : v3, v4, v5,
v6, v7 et v8 — six versions, deux modèles — dont aucune ne déplace ni l'accord ni la conformité.
**Le prompt n'est pas le levier de ce décodeur**, et il faut cesser d'en chercher un là.

**Ce qui reste indécidable.** Que l'information de partition soit inexploitable par *ce modèle* ne
dit rien d'un autre : l'escalier entier a tourné sur qwen3-8b, et le décodeur validé par la séance D
est ministral. Rejouer la seule marche v4 → v8 sur ministral coûterait deux décodages et dirait si
l'amorçage vers la structure est une propriété du modèle ou de la tâche. Non engagé.

### PRÉ-ENREGISTREMENT — réplication de la marche v4 → v8 sur ministral

Écrit le 2026-08-08 **avant toute mesure**, à la demande de l'auteur, et lève l'indécidable que la
note ci-dessus venait d'ouvrir. Toute la série des prompts décodeurs a tourné sur `qwen/qwen3-8b`,
alors que **le décodeur dont l'équivalence humaine est démontrée** (séance D, Δ apparié 0,000) est
`mistralai/ministral-3-14b-reasoning`. Conclure « la partition est inexploitable » sur le seul qwen
serait conclure sur un modèle qu'on n'utilise pas.

**Un seul décodage à payer.** La référence v4 sur ministral **existe déjà** : `f5bad93aeed3`,
mêmes paramètres, `intra_card_rate` **0,202**, R̄ 0,502. Il ne manque que v8.

**Montage — une seule variable, le modèle.** Banc `416b819a41a1`, run `20260728-…-d79a63b9`, prompt
`decode-clue` **v8 inchangé** (celui du commit `71967fa`, amendement de format compris), temp 0,3 ·
topP 1,0 · maxOut 512, 3 décodages par indice. Le modèle est surchargé par
`DECODER__DEFAULTMODEL`, `evalsettings.json` restant sur qwen — la surcharge est tracée au
manifeste et n'engage pas la configuration du dépôt.

**Lecture, fixée d'avance**, relative à la base ministral de 0,202 et à σ = √(0,2 × 0,8 / 460) =
0,0186 :

| `intra_card_rate` mesuré | lecture |
|---|---|
| **≤ 0,165** | ministral **exploite** la partition. La piste 2 rouvre — et elle rouvre sur le décodeur qui compte. Résultat majeur. |
| **0,165 – 0,239** | l'étiquette n'amorce pas ministral. L'amorçage observé est une propriété de **qwen**, pas de la tâche. |
| **≥ 0,239** | l'amorçage est une propriété de **la tâche**. La conclusion de la marche v8 se généralise, la piste 2 est close pour de bon. |

**Prédiction posée d'avance : `intra_card_rate ≈ 0,27`, donc troisième case.** Raison : sur qwen
l'étiquette a multiplié la violation par 1,60 (0,196 → 0,314) ; appliqué tel quel à 0,202 cela
donnerait 0,323. J'attends un effet **atténué** — ministral est plus gros, mieux noté sur la tâche
(R̄ 0,502 contre 0,397) et devrait résister davantage à un attracteur typographique — mais de même
signe, car rien dans la série ne montre un modèle tirant profit de la structure. Δ`recovery`
apparié contre `f5bad93aeed3` en témoin secondaire.

**Contrôle obligatoire, propre à ce modèle.** `ministral-3-14b-reasoning` **n'expose aucun toggle
thinking** et pense par défaut ; sous les prompts décodeurs il rend pourtant `reasoning_tokens = 0`,
inhibé par leur contrainte de format. v8 ne relâche pas cette contrainte, mais il en change le bloc
de données : **sonder `reasoning_tokens` au premier appel**. Un réveil du thinking invaliderait la
comparaison à `f5bad93aeed3`, `maxOutputTokens = 512` ne le bornant pas.
| 2026-08-08 | 20260728-v5-google-gemma-4-12b-qat-d79a63b9 | boards.dev.jsonl | board-clues-per-direction.md | v5 | google/gemma-4-12b-qat | 2026-07-28 | temp 1 / topP 0,95 / maxTokens 4096 / maxRetries 0 / reasoning False | 0,963 | 0,963 | 0,455 | 0,039 | 0,688 | 0,000 | pré-calibration | neutre | replication v4 -> v8 sur ministral : intra_card_rate 0,232 contre 0,202, soit 1,6 sigma - NON etabli. L'amorcage massif observe sur qwen (+0,118, 6,3 sigma) est une propriete du modele, pas de la tache | gén. : gemma-4-12b-qat thinking OFF, LM Studio JIT ; déc. : mistralai/ministral-3-14b-reasoning |

### RÉSULTAT — réplication sur ministral, 2026-08-08 — **l'amorçage est propre au modèle, mais la piste 2 reste close**

Décodage lancé à 19:19, terminé en **10 min 07**, empreinte `f777466a03b6`. Sonde préalable sur le
prompt v8 réel : `reasoning_tokens = 0`, 902 tokens de prompt — le thinking natif reste inhibé par
la contrainte de format, la comparaison à `f5bad93aeed3` est valide. Prompt v8 inchangé au
caractère près, seule la surcharge `DECODER__DEFAULTMODEL` diffère du run qwen.

| modèle | prompt | `intra_card_rate` | `recovery` | `half_rate` | `strict_2of2` | part de `r = 1` |
|---|---|---|---|---|---|---|
| ministral | v4 `f5bad93aeed3` | **0,202** (93/460) | 0,483 | 0,792 | 0,052 | 0,130 |
| ministral | v8 `f777466a03b6` | **0,232** (107/462) | 0,455 | 0,688 | 0,039 | 0,128 |
| qwen | v4 `e46ee636933a` | 0,196 (90/460) | 0,382 | 0,635 | 0,032 | 0,080 |
| qwen | v8 `1143466d9456` | 0,314 (145/462) | 0,369 | 0,610 | 0,039 | 0,082 |

**La grille pré-enregistrée range le résultat en deuxième case, et ma prédiction est fausse.**
J'avais écrit 0,27, donc troisième case (« l'amorçage est une propriété de la tâche ») ; on mesure
**0,232**, qui tombe dans l'intervalle 0,165 – 0,239, soit **« l'amorçage est une propriété de
qwen »**. La prédiction se trompe de 2 σ. Elle avait tenu sur qwen, elle tombe ici : une sur deux.

**Réserve à ne pas taire : c'est un cas limite.** 0,232 n'est qu'à 0,007 du seuil de la troisième
case (0,239), soit **0,4 σ**. La grille tranche parce qu'elle était écrite d'avance et qu'on
l'applique telle quelle — mais les données, elles, ne séparent pas nettement les cases 2 et 3. Ce
qui *est* établi tient dans la comparaison des deux modèles, pas dans le placement d'un seuil :

- sur **qwen**, l'étiquette porte la violation de 0,196 à 0,314 — **+0,118, soit 6,3 σ**, massif ;
- sur **ministral**, de 0,202 à 0,232 — **+0,030, soit 1,6 σ, non établi**.

Un facteur **quatre** entre les deux amplitudes, à prompt rigoureusement identique. L'attracteur
typographique est donc très largement une propriété du **modèle** : le 14B y résiste là où le 8B y
succombe. C'est un acquis réutilisable au-delà de cette marche — un décodeur plus gros est plus
robuste aux artefacts de présentation, et une conclusion de prompt tirée sur qwen seul ne se
transporte pas.

**Mais la piste 2 ne rouvre pas, et c'est le point qui compte.** La première case demandait
`intra_card_rate ≤ 0,165` pour conclure que ministral **exploite** la partition. Il ne l'exploite
pas : il monte, faiblement mais il monte. Aucun des deux modèles ne tire profit de l'information de
carte. La conclusion de la marche v8 se généralise donc — **pour des raisons différentes** selon le
modèle, ce qui n'était pas prévisible : qwen est amorcé vers la structure, ministral y est
indifférent.

**Et v8 coûte à ministral, plus nettement qu'à qwen.** Δ`recovery` apparié **−2,8 pts, IC 95 %
[−5,2 ; −0,4]** : l'intervalle est **entièrement sous zéro**. Le verdict `NEUTRE` rendu par le
harnais est un seuil **pratique** (3 pts) et non statistique — à ne pas lire comme « pas d'effet ».
Sur qwen l'IC contenait zéro ; ici non. Le dégât se voit surtout au milieu de l'échelle :
`half_rate` chute de **0,792 à 0,688** (−10,4 pts) tandis que la part de `r = 1` ne bouge pas
(0,130 → 0,128). L'étiquette ne coûte pas des récupérations exactes à ministral, elle transforme
des demi-récupérations en échecs francs.

**Deux contrôles tenus.** `decode_failure_rate` côté `decode-clue` vaut **0,000** (0/462), contre
0,004 sous v4 — la précision de format tient sur ce modèle aussi. Et l'invariant reste intact :
**0 décodage intra-carte à `r = 1`** sur les 107 de ce lot, portant le cumul à **455 sur 455**,
trois modèles et six prompts.

**Post-stratification, pour mémoire** : v8 ministral gagnerait +0,015 sur R̄ et passerait de 0,128 à
0,166 sur `r = 1` — même profil que v4 ministral (+0,017 et 0,130 → 0,163). Le resampling ne
rattrape pas ce que la présentation coûte.

**Décision.** v8 est écartée sur ministral comme sur qwen ; `decode-clue.md` **reste en v4**, v8
archivée au commit `71967fa` et rejouable. **La piste 2 est close sur les deux modèles.** Avec
elle, la série des prompts décodeurs : v3, v4, v5, v6, v7, v8 — six versions, **deux modèles**,
aucune ne déplace ni l'accord, ni la conformité, ni le `recovery` dans le bon sens. Il n'y a plus
de raison de chercher un levier du côté du prompt décodeur.

**Ce qui reste indécidable.** Pourquoi ministral résiste à l'attracteur que qwen subit n'est pas
expliqué — taille, entraînement, quantization identique (Q4_K_M) mais architecture différente. La
question n'est pas nécessaire à la suite du chantier et n'est pas ouverte comme piste. Reste
entière, en revanche, la décision sur le resampling : son gain est de +0,015 sur R̄ et +25 % en
relatif sur `r = 1`, et rien dans cette séance ne l'a rendue plus urgente ni moins risquée.

## P7 — baseline officielle, plafond humain, taxonomie

### DÉCISION D'OUVERTURE — 2026-08-08 — **on entre en P7 avec un instrument non calibré, et on l'écrit**

Écrite **avant toute commande de P7**. Elle fixe le décodeur, le statut des lignes à venir, la règle
d'arbitrage et ce qui n'est pas consulté. Rien ici n'est négociable après avoir vu un chiffre.

**Décodeur de référence : `f5bad93aeed3`** — `mistralai/ministral-3-14b-reasoning`, `decode-clue`
v4, temp 0,3, topP 1,0, maxOut 512, 3 décodages/indice. Motif : c'est le **seul décodeur dont la
devinette a été montrée interchangeable avec celle d'un humain** (séance D du 2026-08-07, 42
directions appariées, Δ = 0,000 IC 95 % [−0,063 ; +0,063]). Les trois runs dev — baseline v5,
plancher aléatoire, pseudo-run humain — sont **déjà décodés sous cette empreinte** : P7 ne coûte
**aucun appel LLM**, et les trois lectures portent structurellement sur le même instrument.

**Statut des lignes : `pré-calibration`, et c'est un résultat, pas un oubli.** Les quatre portes ne
sont pas franchies. Sur **neuf** calibrations, l'accord brut va de 0,490 à 0,667 pour un seuil à
0,75, et κ n'a jamais atteint 0,40. Les deux portes d'échelle le sont, elles, sous cette empreinte
même : plancher 0,125 ✓ et non-saturation 0,576 ✓. **Aucun statut `calibré` n'est donc demandé** :
`score --calibration` n'est pas passé, le harnais refuserait de toute façon. Le design le prévoit
nommément — §10, critère 3 : « ou, si les portes sont tombées, une ligne consignant l'échec ».

**Ce que ce statut coûte, exactement.** Aucun `recovery` de ce registre n'est défendable **en valeur
absolue**. Ce qui le reste, ce sont les lectures **appariées, sur les mêmes items, sous la même
empreinte** — et c'est précisément la forme du plafond humain et de la taxonomie. P7 est donc
conduit **en lecture relative uniquement**, et toute phrase de la forme « v5 récupère 48 % » est
hors registre.

**Ce qui autorise à avancer malgré une porte tombée — et pourquoi ce n'est pas la contourner.** La
séance D établit que la porte d'accord demandait au décodeur de **classer** deux indices, exercice
que le jeu ne contient pas, alors qu'il **devine** aussi bien qu'un humain. Le seuil n'est pas
baissé : il reste inscrit en échec dans la table des calibrations, et la garde 6 est respectée à la
lettre — on ne rediscute pas 0,75, on avance sur une propriété **mesurée séparément**. Ce que la
séance D ne fait pas non plus : refonder la porte. Il y manque Δ(H1, H2), l'écart entre deux
humains, jamais mesuré ; la piste 1 (séance E) reste ouverte et **P7 ne la consomme pas**.

**Règle d'arbitrage, pré-enregistrée avant tout calcul.** L'arbitre de §5.6 est le **Δ apparié**
baseline v5 ↔ humain sur les **40 directions de la séance A, toutes issues** (`pass` compris, A-1),
avec son IC bootstrap. Lecture secondaire sur les 22 `solide` (A-3). Seuil du design : v5 à **≥ 90 %
du plafond** ⟹ « plafond atteint, arrêter d'investir » ; nettement en dessous avec un mode d'échec
≥ 5 % ⟹ « marge réelle » ; portes de justesse ou plafond humain lui-même bas ⟹ « instrument à bout ».

**Ce que je sais déjà, et qui ne décide rien.** Les `.metrics.json` sous cette empreinte portent
`recovery` v5 = 0,483 sur 160 directions et plafond `solide` = 0,576 sur 22. Leur rapport (0,84)
**n'est pas une lecture valide** : dénominateurs différents, items différents, aucun appariement.
Il est écrit ici pour qu'on ne puisse pas le présenter plus tard comme une prédiction confirmée.

**Prédiction.** Plafond humain **joué** ≈ 0,55 (sous qwen v2 il valait 0,333 pour un `solide` à
0,341 : les deux lectures se tiennent de près). Δ apparié humain − v5 sur les 40 directions ≈ **+0,07
pt**, soit v5 à ~87 % du plafond — donc issue « **marge réelle** ». Réserve déclarée d'avance : à
n = 40 l'IC vaudra environ ±0,10, il **recouvrira vraisemblablement le seuil des 90 %**, et dans ce
cas l'issue ne sera pas départagée par le chiffre seul — elle le sera par la taxonomie, qui a son
propre critère à 5 %.

**Test set non consulté.** `eval/boards.test.jsonl` n'est pas ouvert en P7 : aucune variante n'a été
promue, il n'y a rien à généraliser, et le premier ancrage test se mesurera **apparié** à la première
variante promue, sur les mêmes items. Décision datée, conforme à §5.5.
