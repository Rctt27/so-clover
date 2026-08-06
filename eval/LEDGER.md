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
