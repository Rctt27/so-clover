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
