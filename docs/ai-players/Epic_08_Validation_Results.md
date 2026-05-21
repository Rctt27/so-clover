# Epic 08 — Résultats de validation manuelle

Template à remplir au fil des runs LM Studio (Task 9) et Anthropic (Task 10)
du plan `docs/superpowers/plans/2026-05-13-epic-08-real-provider-integration.md`.

## Critères d'acceptation (rappel)

- A. **Smoke LM Studio** : 1 game 2H+1AI génère 4 indices via LM Studio en
  < 60 s sans intervention humaine.
- B. **Smoke Anthropic** : 1 game 2H+1AI génère 4 indices via Anthropic en
  < 10 s.
- C. **Batch ≥ 80 %** : sur 10 parties test (40 directions au total), au
  moins 32 directions sont valides au 1ᵉʳ essai (`attempt=0`).
- D. **MaxConcurrency=4 Anthropic** : 4 AIs en parallèle produisent leurs
  16 clues sans 429 et sans sérialisation perceptible (4 LLM call
  completed logs avec timestamps qui se recouvrent).

## 1. Procédure (à exécuter par l'opérateur)

### Smoke local

1. Lancer LM Studio, charger le modèle FR retenu, démarrer le serveur.
2. `dotnet run --project SoClover` (env vide → lit
   `appsettings.Development.json`).
3. Suivre les commandes curl de `Operations_AI_Players.md §4`.
4. Chronométrer entre `POST /start` et le 4ᵉ log "AI clue validated".
5. Capturer 6 lignes de log (1 LLM call + 4 validated + 0-N rejected).
6. Remplir le tableau Section 2.

### Smoke Anthropic + batch

1. `dotnet user-secrets set "Llm:Provider" "Anthropic" --project SoClover`
   + `Llm:DefaultModel` + `Llm:ApiKey`.
2. `dotnet run --project SoClover`.
3. Smoke : 1 game 2H+1AI. Chronométrer.
4. Batch : exécuter 10 games consécutifs (script bash ci-dessous) avec
   collecte des logs.

```bash
mkdir -p /tmp/epic08-logs
for i in $(seq 1 10); do
  echo "=== Run $i ==="
  # (...sequence de curl créant game + 2H + 1AI + start, attendre 30s, fetch state...)
  # Pipe logs to /tmp/epic08-logs/run-$i.log
done
# Agréger
cat /tmp/epic08-logs/*.log \
  | grep -E "AI clue (validated|rejected)" \
  | grep "attempt=0" \
  | sed -E 's/.*direction=([A-Za-z]+).*isValid=([A-Za-z]+).*/\1,\2/' \
  | sort | uniq -c
```

### MaxConcurrency=4

1. Surcharger : `LLM__MAXCONCURRENCY=4 dotnet run --project SoClover` (déjà
   défaut prod).
2. Lancer 1 game 2H + 4AI.
3. `POST /start` puis observer les 4 logs "LLM call completed" — leurs
   timestamps doivent recouvrir (pas de sérialisation stricte).
4. Vérifier absence de `429` dans les logs Anthropic.SDK.

## 2. Résultats — LM Studio (smoke 2H+1AI)

| Champ | Valeur |
|---|---|
| Date du run (UTC) | `<À REMPLIR>` |
| Modèle LM Studio | `<À REMPLIR>` (ex. `Qwen/Qwen2.5-7B-Instruct-GGUF`) |
| Machine (CPU/GPU/RAM) | `<À REMPLIR>` |
| `Llm.MaxConcurrency` | 1 |
| `Llm.DefaultTemperature` | 0.7 |
| `PromptVersion` capturé dans les logs | `<À REMPLIR>` |
| Latence appel 1 (ms) | `<À REMPLIR>` |
| Latence totale start → 4 clues (s) | `<À REMPLIR>` |
| Directions validées au 1ᵉʳ essai (sur 4) | `<À REMPLIR>` |
| Rejets observés (règle → nb) | `<À REMPLIR>` |
| **Critère A (< 60 s) — Pass/Fail** | `<À REMPLIR>` |

Observations libres : `<À REMPLIR>`.

## 3. Résultats — Anthropic (smoke + batch + concurrent)

### 3.a Smoke 2H+1AI

| Champ | Valeur |
|---|---|
| Date du run (UTC) | `<À REMPLIR>` |
| Modèle Anthropic | `<À REMPLIR>` (`claude-haiku-4-5` attendu) |
| `Llm.MaxConcurrency` | 4 |
| Latence appel 1 (ms) | `<À REMPLIR>` |
| Latence totale start → 4 clues (s) | `<À REMPLIR>` |
| Directions validées au 1ᵉʳ essai (sur 4) | `<À REMPLIR>` |
| Format de réponse (JSON pur / markdown fences / autre) | `<À REMPLIR>` |
| **Critère B (< 10 s) — Pass/Fail** | `<À REMPLIR>` |

### 3.b Batch 10 parties

| Direction | Validées attempt=0 | Rejetées attempt=0 | Total |
|---|---|---|---|
| Top    | `<X>` | `<Y>` | 10 |
| Right  | `<X>` | `<Y>` | 10 |
| Bottom | `<X>` | `<Y>` | 10 |
| Left   | `<X>` | `<Y>` | 10 |
| **Total** | `<X>` | `<Y>` | 40 |

Taux de réussite global = `<X>/40` = `<%>` %.

Règles de rejet dominantes (top 3) :
1. `<À REMPLIR>`
2. `<À REMPLIR>`
3. `<À REMPLIR>`

Coût observé sur 10 parties (Anthropic dashboard) : `$<À REMPLIR>`.

**Critère C (≥ 80 % = 32/40) — Pass/Fail :** `<À REMPLIR>`.

### 3.c Concurrent (1H+4AI, MaxConcurrency=4)

| Champ | Valeur |
|---|---|
| Date du run (UTC) | `<À REMPLIR>` |
| Nombre de 429 observés | `<À REMPLIR>` |
| Latence du dernier "LLM call completed" depuis `POST /start` (s) | `<À REMPLIR>` |
| Recouvrement temporel observé (oui/non) | `<À REMPLIR>` |
| **Critère D — Pass/Fail** | `<À REMPLIR>` |

## 4. Verdict global

| Critère | Statut |
|---|---|
| A | `<À REMPLIR>` |
| B | `<À REMPLIR>` |
| C | `<À REMPLIR>` |
| D | `<À REMPLIR>` |

**Epic 08 — Statut final :** `<À REMPLIR : SUCCESS / PARTIAL / FAIL>`.

Si PARTIAL ou FAIL : décrire l'action de suivi recommandée (itération
prompt, changement de modèle, montée de tier Anthropic, etc.).
