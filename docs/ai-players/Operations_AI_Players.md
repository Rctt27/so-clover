# Operations — AI Players

Guide opérateur pour faire tourner la pipeline AI (Epics 01-07) contre un vrai
LLM. Public cible : un dev qui prend la main sur le projet et veut soit
exécuter le POC en local (LM Studio), soit déployer en prod (Anthropic), soit
réaliser une campagne de validation manuelle (Epic 08).

## 1. Choix du provider

| Mode | Provider | Config | Coût | Latence cible |
|---|---|---|---|---|
| Dev local | LM Studio (OpenAI-compatible) | `appsettings.Development.json` | 0 € | < 60 s pour 4 indices |
| Prod | Anthropic | `appsettings.Production.json` + `LLM__APIKEY` | ~$0.001/partie (Haiku) | < 10 s pour 4 indices |

Bascule = changer `Llm:Provider` dans la config OU surcharger via env var
`LLM__PROVIDER=Anthropic`. Aucun rebuild requis.

## 2. Installer LM Studio (dev local)

1. Télécharger LM Studio : <https://lmstudio.ai>.
2. Lancer LM Studio. Onglet "Discover" → chercher un modèle FR de 7-8 B
   paramètres. Recommandés (à ordre décroissant de qualité FR observée) :
   - `Qwen/Qwen2.5-7B-Instruct-GGUF` (Q4_K_M, ~4 Go)
   - `mistralai/Mistral-7B-Instruct-v0.3-GGUF`
   - `lmstudio-community/Meta-Llama-3.1-8B-Instruct-GGUF` (défaut actuel)
3. Télécharger le modèle (clic "Download").
4. Onglet "Local Server" → sélectionner le modèle chargé → cliquer "Start
   Server". Vérifier l'endpoint affiché (`http://localhost:1234/v1` par
   défaut).
5. Le nom du modèle exposé par l'API est visible dans l'UI ("Loaded Models").
   C'est ce nom qu'il faut copier dans `Llm:DefaultModel`.

### Override ponctuel sans toucher au repo

```bash
LLM__DEFAULTMODEL="Qwen/Qwen2.5-7B-Instruct-GGUF" dotnet run --project SoClover
```

## 3. Configurer Anthropic (prod ou test local)

1. Créer un compte sur <https://console.anthropic.com> et générer une clé API
   (format `sk-ant-...`).
2. Au moins **Tier 1** (~5 $ de crédit initial) est requis pour
   `MaxConcurrency=4` — le tier Free (5 RPM) ne tient pas la cadence en
   batch.
3. Stocker la clé :
   - **Prod (déploiement)** : variable d'environnement `LLM__APIKEY` (cf.
     `SoClover/.env`).
   - **Dev (test ponctuel)** :
     ```bash
     dotnet user-secrets set "Llm:Provider"     "Anthropic" --project SoClover
     dotnet user-secrets set "Llm:DefaultModel" "claude-haiku-4-5" --project SoClover
     dotnet user-secrets set "Llm:ApiKey"       "sk-ant-..." --project SoClover
     dotnet run --project SoClover
     ```
4. Choix du modèle :
   - `claude-haiku-4-5` (défaut prod) : latence < 5 s, ~$0.001/partie.
     Recommandé POC.
   - `claude-sonnet-4-6` : qualité supérieure, ~5×$ et 2× latence. Basculer
     uniquement si Haiku échoue les acceptance criteria sur 10 parties.

## 4. Lancer une partie avec un AI (CLI)

```bash
# Terminal 1 — backend
dotnet run --project SoClover

# Terminal 2 — souscription temps réel (optionnel, pratique pour observer)
npx wscat -c ws://localhost:5000/hubs/game
# Une fois connecté :
{"protocol":"json","version":1}<RS>
{"type":1,"target":"SubscribeToGame","arguments":["<gameId>"]}<RS>
# (<RS> = caractère 0x1E ; wscat le génère via Ctrl+^ + Enter — voir doc wscat)

# Terminal 3 — orchestrer la partie en HTTP
GAME=$(curl -s -X POST http://localhost:5000/api/games \
  -H "Content-Type: application/json" \
  -d '{"playerName":"Alice","language":"Français_OFF"}')
echo "$GAME"
GAME_ID=$(echo "$GAME" | jq -r .gameId)
ADMIN=$(echo "$GAME" | jq -r .playerId)

# Ajout d'un 2ᵉ joueur humain
curl -s -X POST "http://localhost:5000/api/games/$GAME_ID/join" \
  -H "Content-Type: application/json" \
  -d '{"playerName":"Bob"}'

# Ajout d'un AI
curl -s -X POST "http://localhost:5000/api/games/$GAME_ID/ai-players" \
  -H "Content-Type: application/json" \
  -d "{\"adminPlayerId\":\"$ADMIN\",\"playerName\":\"BotZoé\"}"

# Démarrer la phase d'écriture (déclenche la génération AI)
curl -s -X POST "http://localhost:5000/api/games/$GAME_ID/start"

# Observer l'état (les 4 clues du bot apparaissent en ~5-60s)
curl -s "http://localhost:5000/api/games/$GAME_ID/state" | jq '.players[] | select(.isAI) | .board'
```

## 5. Lire les logs structurés

Chaque tentative LLM produit 2 lignes (côté console backend) avec ce template
nommé :

```
info: SoClover.UseCases.AI.GenerateAIClues.Handler[0]
      AI clue LLM call completed: game=<id> player=<id> attempt=0 latencyMs=4231 provider=Anthropic model=claude-haiku-4-5 promptVersion=2 remainingDirections=Top,Right,Bottom,Left
info: SoClover.UseCases.AI.GenerateAIClues.Handler[0]
      AI clue validated: game=<id> player=<id> direction=Top clueText=NUAGE isValid=True promptVersion=2 provider=Anthropic model=claude-haiku-4-5
info: SoClover.UseCases.AI.GenerateAIClues.Handler[0]
      AI clue rejected: game=<id> player=<id> direction=Right clueText=PLUIE isValid=False rejectionRules=Synonym promptVersion=2 provider=Anthropic model=claude-haiku-4-5
```

Pour quantifier le taux de réussite par direction sur N parties :

```bash
# Extraire les rejets/validations dans un CSV minimal (logs console)
dotnet run --project SoClover 2>&1 \
  | grep -E "AI clue (validated|rejected)" \
  | sed -E 's/.*direction=([A-Za-z]+).*isValid=([A-Za-z]+).*promptVersion=([0-9]+).*/\3,\1,\2/' \
  > clue_outcomes.csv
```

Pour un déploiement réel, brancher un sink structuré (Seq, Application
Insights, Loki). Les propriétés `IsValid`, `PromptVersion`, `LlmProvider`,
`LlmModel`, `Direction`, `LatencyMs` sont déjà exposées par le logger
framework — aucune modification de code n'est nécessaire.

## 6. Pièges connus

- **JSON markdown fences** : certains modèles locaux (notamment Llama 3.1)
  renvoient le JSON enrobé de \`\`\`json … \`\`\`. Le `JsonSerializer` lève
  alors `LLM returned invalid JSON`. Mitigations : (a) renforcer le prompt
  ("Réponds UNIQUEMENT en JSON sans markdown"), (b) basculer sur Qwen ou
  Mistral, (c) accepter le risque (3 retries → souvent le 2ᵉ essai est
  conforme).
- **Anthropic rate-limit** : Tier 1 = 50 RPM. Avec 4 AIs × 3 retries possibles
  = jusqu'à 12 appels/10s soit ~72 RPM. Si 429, baisser `MaxConcurrency` à 2.
- **LM Studio CPU thermal throttling** : la latence peut tripler au-delà de
  10 min de batch continu sur laptop. Pauses recommandées entre runs.

## 7. Régénération manuelle

Pas d'endpoint dédié en POC. Pour régénérer les clues d'un AI, supprimer la
game et la recréer (l'endpoint `regenerate-clues` est explicitement en
out-of-scope POC — cf. `00_Overview.md`).
