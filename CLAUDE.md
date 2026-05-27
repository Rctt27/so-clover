# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SoClover is a real-time multiplayer implementation of the "So Clover!" board game. Built with ASP.NET Core 9.0 backend, React 18 frontend, and SignalR for real-time communication.

## Versioning

Le projet suit **SemVer** (`vMAJOR.MINOR.PATCH`), avec des tags Git annotés et des GitHub Releases.
- **MAJOR** : changement structurel majeur (ex: réécriture du front-end).
- **MINOR** : nouvelle feature notable.
- **PATCH** : correctifs / durcissements sans nouvelle feature.

**Version courante : `2.8.0`.**

Jalons structurants : réécriture du front en React/TypeScript (v2.0), temps réel SignalR (v1.3), persistance PostgreSQL (v1.2), joueurs IA (v2.5), support du dictionnaire Anglais (v2.6), validation sémantique des indices étendue à l'Anglais (v2.7). Historique complet des tags : voir [`CHANGELOG.md`](CHANGELOG.md).

### Processus à chaque nouvelle release (OBLIGATOIRE)
1. Mettre à jour `CONSTANTS.APP_VERSION` dans `SoClover/client/src/core/constants.ts` — c'est cette
   valeur qui s'affiche dans le footer de l'écran d'accueil (`components/home/HomeScreen.tsx`).
2. Mettre à jour la « Version courante » ci-dessus.
3. **Mettre à jour `CHANGELOG.md`** : ajouter une ligne en bas du tableau au format
   `| vX.Y.Z | \`<commit-court>\` | YYYY-MM-DD | <jalon : résumé concis de la feature/du correctif> |`.
   Le `<commit-court>` est le hash du commit de release (`git rev-parse --short HEAD`), la date est celle du jour.
   Si le jalon est structurant (réécriture, nouvelle brique majeure), l'ajouter aussi à la phrase de résumé
   « Jalons structurants » de la section Versioning ci-dessus.
4. Créer le tag annoté et la release : `git tag -a vX.Y.Z -m "..."` puis `gh release create vX.Y.Z`.

## Commands

### Backend (.NET)
```bash
dotnet build                                    # Build solution
dotnet run --project SoClover                   # Run application
dotnet watch --project SoClover                 # Watch mode
dotnet test                                     # Run all tests
dotnet test --filter "FullyQualifiedName~TestName"  # Run specific test
```

### Frontend (from SoClover/client/)
```bash
npm install        # Install dependencies
npm run dev        # Vite dev server (proxies to localhost:5000)
npm run build      # Production build
npm run lint       # ESLint (strict, no warnings)
```

### EF Core Migrations
```bash
dotnet ef migrations add Name --project SoClover --startup-project SoClover
dotnet ef database update --project SoClover
```

### Docker
```bash
# Depuis SoClover/ — profil Production (Anthropic)
docker compose --env-file .env build --no-cache web
docker compose --env-file .env up -d

# Profil Development (LM Studio) via override
docker compose -f compose.yaml -f compose.dev.yaml --env-file .env.dev up -d
```
- `compose.yaml` est la base prod-ready. `compose.dev.yaml` est un override qui injecte `DOTNET_ENVIRONMENT=Development` et `LLM__BASEURL=http://host.docker.internal:1234/v1` pour parler à LM Studio sur l'hôte.
- Les secrets (PostgreSQL, `LLM__APIKEY`) viennent de `SoClover/.env` ou `.env.dev` (jamais committés).

### Développement local (full-stack)
```bash
# Terminal 1 — Backend
dotnet watch --project SoClover
# Terminal 2 — Frontend (depuis SoClover/client/)
npm run dev   # Proxy automatique vers localhost:5000
```

## Architecture

### Backend Structure
- **Domain/**: Pure business logic. `Game` is the root aggregate. Contains entities (Player, Board, Card), value objects (GameId, PlayerId), and enums.
- **UseCases/**: Command/Handler pattern. `GameLogics/` for core flow, `Gameplay/` for advanced orchestration.
- **Infrastructure/**: Technical implementations - EF Core persistence, SignalR events, file-based dictionaries.
- **RealTime/**: SignalR hub (`GameHub.cs`).

### Frontend Structure (SoClover/client/)
- **components/**: Page-based organization (home, lobby, writing, guessing, scoring) + shared components.
- **components/guards/**: `RoleGuard` — protège les routes selon le rôle/phase du joueur.
- **features/**: Feature modules (ex: `mouseTracking/` — suivi curseur temps réel via SignalR).
- **core/**: Zustand slices (boardSlice, guessingSlice, notificationSlice), helpers, constants.
- **hooks/**: useSignalR, useGameActions, useGameStateUpdate, usePermissions, useNotifications, useTimeoutSafetyPolling, useWritingCluesPhaseMusic.
- **api/**: HTTP client (game-api.ts) and SignalR client (signalr-client.ts).
- **types/**: TypeScript definitions (game.ts).

### Key Patterns
- **Document Store**: Game state persisted as JSON in PostgreSQL (JSONB column).
- **Repository Pattern**: `InMemoryGameRepository` (DEBUG) / `EfGameRepository` (RELEASE).
- **Event Publishing**: Domain actions → `IEventPublisher` → `SignalREventPublisher` → Client updates.
- **State Machine**: Lobby → WritingClues → Guessing → Scoring.
- **Zustand Persist**: Le store global utilise `persist` middleware (localStorage). En cas d'état incohérent lors du debug, vider le localStorage peut être nécessaire.
- **Mouse Tracking**: Suivi des curseurs joueurs via SignalR — `features/mouseTracking/` côté client.

### Game Flow
1. Create game → Players join lobby. Only Admin player can update game settings and add AI players.
2. Start writing phase → Each player writes clues for their board
3. Start guessing phase → Players guess card placements on others' boards
4. Scoring → Display results

### AI Players

- **Feature flag** : section `AIPlayers.Enabled` dans `appsettings*.json` (`true` en dev, `false` en prod par défaut). Quand désactivé, le bouton "+ Ajouter un joueur IA" du lobby est grisé avec tooltip, et l'endpoint `POST /api/games/{id}/ai-players` renvoie 403 (`AIPlayersDisabledException`). Le client lit le flag via `GET /api/config` au boot (slice `appConfigSlice`).
- **Provider switch** : `appsettings.Development.json` (`Provider=OpenAI`, `BaseUrl=http://localhost:1234/v1`, `MaxConcurrency=1`) vs `appsettings.Production.json` (`Provider=Anthropic`, `DefaultModel=claude-haiku-4-5`, `MaxConcurrency=4`). Choix via `DOTNET_ENVIRONMENT` (défaut `Production`). Pour tester le profil Dev depuis Docker, utiliser l'override `compose.dev.yaml` (cf. section Docker plus haut) — il injecte `DOTNET_ENVIRONMENT=Development` et `LLM__BASEURL=http://host.docker.internal:1234/v1` (le `localhost:1234` d'appsettings est inaccessible depuis un conteneur). La seule différence runtime est le binding de `IChatClient` via `ChatClientFactory` (`SoClover/Infrastructure/AI/ChatClientFactory.cs`).
- **Secret LLM** : `LLM__APIKEY` est le **seul** paramètre LLM qui vit dans `SoClover/.env` (cf. règle de configuration ci-dessous). En dev pour tester Anthropic : `dotnet user-secrets set "Llm:ApiKey" "sk-ant-..." --project SoClover`. Ne **jamais** committer la clé.
- **LM Studio** : application desktop, expose un serveur OpenAI-compatible sur `http://localhost:1234/v1` (port configurable dans l'UI). L'`ApiKey` n'est pas vérifiée — `"lm-studio"` factice suffit mais doit être non-vide (`LlmOptionsValidator`).
- **Structured logs** : chaque appel LLM produit 1 log "AI clue LLM call completed" (`LatencyMs`, `Provider`, `Model`, `PromptVersion`, `Attempt`, `RemainingDirections`). Chaque clue validée/rejetée produit 1 log avec `IsValid` + `RejectionRules`. Utiliser ces props pour comparer 2 versions de prompt (`PromptVersion` = champ `version:` du frontmatter de `Infrastructure/AI/Prompts/<lang>/*.md`).
- **Mode reasoning (togglable, agnostique)** : flag `Llm.ReasoningEnabled` (défaut `false`, surchargeable `LLM__REASONINGENABLED`). Deux modes pour un A/B :
  - **OFF** : prompt prescriptif baseline (la procédure en 7 étapes est exécutée « en interne », sortie = JSON uniquement). Aucun paramètre natif passé.
  - **ON** : la section `# REASONING` (advisory) de `Prompts/<lang>/board-clues.md` est appendée au system prompt — elle lève l'interdiction « JSON only » et transforme la procédure en **guide** pour le canal de raisonnement natif (FR et EN synchronisés). En plus, `IReasoningRequestConfigurator` (`Infrastructure/AI/Reasoning/`) injecte les paramètres natifs du provider via `ChatOptions.RawRepresentationFactory` : `Llm.ReasoningEffort` (`low`/`medium`/`high`, OpenAI/o-series/LM Studio) et `Llm.ThinkingBudgetTokens` (Anthropic extended thinking). Le choix de l'impl (Null/OpenAI/Anthropic) se fait au câblage DI dans `Program.cs` selon `Provider` + `ReasoningEnabled`.
  - **Trigger système spécifique au modèle** (`Llm.ReasoningSystemPromptPath`) : certains modèles reasoning n'activent leur raisonnement **que si leur system prompt officiel est présent** (ex. Mistral `Ministral-3-14B-Reasoning` → `SYSTEM_PROMPT.txt`, contenant un exemple `[THINK]…[/THINK]`). Sans lui, `reasoning_effort` est ignoré (LM Studio loggue « No valid custom reasoning fields … cannot be converted to any custom KVs »). Renseigner le chemin vers ce fichier : son contenu est **préfixé** au system prompt quand le mode reasoning est actif (lecture tolérante : fichier absent → warning + génération sans préambule). Agnostique : un autre modèle → son propre fichier, ou rien.
  - **LM Studio** : l'activation du reasoning natif est surtout **côté serveur** (« Enable Thinking » + « Reasoning Parsing ») et/ou via le trigger système ci-dessus. `ReasoningEffort` n'est honoré que par les modèles déclarant des custom KVs (gpt-oss…). Avec parsing natif ON, le raisonnement part dans `reasoning_content` et `content` reste du JSON propre. Fallback : `GenerateAIClues.StripThinkTags` retire un `<think>...</think>` **ou** `[THINK]...[/THINK]` inliné dans `content`. Sampling recommandé pour Ministral-Reasoning : temp 1.0 / top_p 0.95.
- **Procédure opérateur complète** : `docs/ai-players/Operations_AI_Players.md`. Résultats de validation : `docs/ai-players/Epic_08_Validation_Results.md`.
- **Troubleshooting** :
  - `Connection refused: localhost:1234` → LM Studio pas démarré ou port différent (Settings → Server).
  - `LlmBudgetExhaustedException` → `Llm.maxCallsPerGame` atteint (défaut 200 dans `appsettings.json`).
  - `LLM returned invalid JSON` → modèle local trop faible. Tester un autre modèle ou baisser `defaultTemperature` à 0.3.
  - Anthropic 429 → rate-limit du tier ; baisser `maxConcurrency` à 2 ou monter de tier.

## Testing

Key test files:
- `FullGameFlowTests.cs` - Complete happy path through all phases
- `BreakingGameTests.cs` - Edge cases and error handling
- `DomainRotationTests.cs` - Card rotation logic

Tests use `TestClock` for time control and `InMemoryGameRepository` for isolation.

## Local Docker End-to-end testing

Developer needs to perform end-to-end testing on local dev env using Docker before push new code
```bash
docker compose -f compose.yaml -f compose.dev.yaml --env-file .env.dev build --no-cache
docker compose -f compose.yaml -f compose.dev.yaml --env-file .env.dev up -d
```

## Configuration

**Règle directrice — qui met quoi** (pattern .NET idiomatique multi-couches) :
- `appsettings.json` : défauts partagés, non-secrets, communs à tous les environnements (`GameDefaults`, tuning `Llm` : `defaultTemperature`, `topP`, `maxOutputTokens`, `maxRetries`, `timeoutSeconds`, `maxCallsPerGame`). `topP` et `maxOutputTokens` sont nullables (null = défaut du provider) et appliqués sur le `ChatOptions` de chaque appel (`GenerateAIClues.CallLlmAsync`). `defaultTemperature` sert de défaut quand le joueur IA n'a pas de température explicite dans son `AIConfig`. Pour les modèles reasoning Mistral, reco : `temp 1.0 / topP 0.95` + `maxRetries 0` (un run reasoning échoué coûte cher, inutile de retenter ×3).
- `appsettings.{Environment}.json` : overrides non-secrets spécifiques à un environnement (`Llm.Provider/BaseUrl/DefaultModel/MaxConcurrency`, `AIPlayers.Enabled`). Versionné, auditable.
- `SoClover/.env` (et `.env.dev`) : secrets uniquement (`LLM__APIKEY`, `POSTGRES_*`) et vars frontend (`VITE_*`). Jamais committés. Template : `SoClover/.env.example`.
- `SoClover/compose.dev.yaml` : override compose pour lancer le profil Development en Docker (injecte `DOTNET_ENVIRONMENT` et `LLM__BASEURL` vers `host.docker.internal`). Versionné, c'est le seul endroit qui contredit appsettings — par design, puisque l'URL `localhost` y est inutilisable.
- **Échappatoire** : les vars `LLM__*` peuvent overrider n'importe quelle clé `Llm:*` (env vars > appsettings). Pour des expérimentations ponctuelles (autre modèle, autre concurrency), préférer `dotnet user-secrets` plutôt que `.env`.

Autres notes :
- DEBUG mode uses in-memory repository
- RELEASE mode uses PostgreSQL (`DATABASE_URL` or `ConnectionStrings:GameDb`)
- Word dictionaries in `Infrastructure/Dictionaries/` (co-localisés avec `FileWordDictionary`)
- `GameDefaults` exposé via `IOptions<GameDefaultsOptions>` ; `AIPlayers` via `IOptions<AIPlayersOptions>` ; `Llm` via `IOptions<LlmOptions>`.
- Vite lit les vars depuis `SoClover/` (`envDir: '../'` dans `vite.config.ts`) — ne pas créer de `client/.env`
- Debug local : créer `SoClover/.env.local` avec `VITE_DEBUG_MODE=true` (gitignored)

## API Endpoints

Main endpoints in `Program.cs`:
- `POST /api/games` - Create game
- `POST /api/games/{id}/join` - Join game
- `POST /api/games/{id}/start` - Start writing phase
- `POST /api/games/{id}/clues` - Set clue
- `POST /api/games/{id}/start-guessing` - Start guessing phase
- `POST /api/games/{id}/place-guessing-card` - Place card
- `GET /api/games/{id}/state` - Get full game state

SignalR hub at `/hubs/game`.

## Conventions

- C# follows .NET standards: PascalCase for public members, `_camelCase` for private fields.
- Business logic belongs in Domain classes, not UseCases.
- UseCases contain nested `Handler` classes implementing `IUseCase<TRequest, TResponse>`.
- React components use PascalCase, hooks/utilities use camelCase.
- Zustand for state management with separate slices.
- Logs frontend verbeux : utiliser `debugLog(source, message)` de `core/debug.ts` — jamais `console.log` directement.
- Zustand DevTools activés uniquement si `isDebug` (conditionnel sur `VITE_DEBUG_MODE`).

### Frontend – Son & Mute

- Tous les volumes sont des constantes nommées dans `core/sounds.ts` — ne jamais hardcoder un volume directement dans un `new Howl()`.
- État mute stocké dans `localStorage` (`so-clover-muted`) et propagé via `CustomEvent('so-clover-mute-changed')`.
- **Gotcha** : `writingCluesMusic` utilise Web Audio API (`html5: false` par défaut). Ne pas passer en `html5: true` — cela bloquerait silencieusement la lecture depuis un callback SignalR (hors geste utilisateur), car le Web Audio API est déjà déverrouillé par les autres sons de l'app.

### Frontend – Constantes & Configuration

- **Centralisation des constantes** – Toujours ajouter les constantes (timings, dimensions, offsets, seuils, etc.) dans `core/constants.ts` sous la section appropriée (`ASSET_REFERENCES`, `THEME_CONFIG`, etc.) plutôt que dans les fichiers/composants individuels. Cela évite la redondance et facilite la maintenance. Ne jamais dupliquer une valeur magic — si elle existe dans `CONSTANTS`, la déstructurer plutôt que la redéfinir.

### Backend – Gotchas & Patterns

- **HTTP endpoint double-mapping (généralisé)** : plusieurs endpoints HTTP dans `Program.cs` re-mappent manuellement les DTOs des UseCases vers des objets anonymes (notamment `/api/games/{id}/scoring` ↔ `GetScoring.cs:BoardResultDto`, et `/api/games/{id}/state` ↔ `GetGameState.cs:Response/ClueInfo/etc.`). Ajouter un champ au DTO **n'apparaîtra pas dans la réponse HTTP** tant que le mapping anonyme n'est pas mis à jour. Par contre la diffusion SignalR (`SignalREventPublisher.cs`) sérialise directement le record typé — pas de double-mapping côté events.
- **Dépendance UseCase → RealTime interdite** : Ne jamais référencer `GameHub` directement depuis un UseCase. Utiliser une interface injectable (ex. `IConnectionTracker` dans `SoClover/RealTime/`) avec injection optionnelle (`= null`) — les tests passent sans l'enregistrer, le runtime injecte l'implémentation réelle.
- **`ActivePlayers` vs `Players`** : `game.ActivePlayers` exclut les joueurs déconnectés (`IsDisconnected = true`). Toute logique de flux (SubmitBoard, StartGuessingPhase, MoveToNextBoard, MoveToNextGuessingBoard) doit utiliser `ActivePlayers`. `game.Players` reste pour le scoring et l'affichage complet.
- **Revision protocol (sync)** : `Game.Revision` est monotone (bumpée lors des mutations). Les events `BoardRotated` et `GameStateUpdated` la portent. Le client drop les events de révision ≤ celle déjà appliquée — remplace l'ancien anti-echo timing-based de 500ms. Toute nouvelle mutation domaine touchant un board doit bumper Revision et les events doivent la propager.

### Frontend – Design & Assets

- **Avant tout travail visuel**, explorer `SoClover/client/src/assets/styles/` pour réutiliser les variables CSS et styles existants.
- Les couleurs, espacements, animations et autres tokens visuels doivent être centralisés dans `assets/styles/` — jamais hardcodés inline dans les composants.
- Si une valeur visuelle (ex: palette de confettis, timing d'animation) n'existe pas encore dans les styles centralisés, la créer dans le fichier approprié de `assets/styles/` avant de l'utiliser dans le composant.
- Réutiliser les assets existants (`public/sounds/`, `public/images/`) plutôt que d'en embarquer de nouveaux sans vérification préalable.

### Frontend – Sync & Performance

- **Revision tracking** : `guessingSlice.lastAppliedRotationRevision` utilise un setter monotone (jamais réécrit en arrière). Vérifier la révision avant d'appliquer un event de rotation — ne pas réintroduire d'anti-echo timing-based.
- **`rotationGapDetector`** : warn si la séquence de révisions saute un event (observabilité du flux SignalR). À utiliser pour tout nouveau flux event-driven séquencé.
- **Memo comparators extraits** : pour `React.memo()` non-trivial, extraire le comparator dans un fichier dédié (ex. `draggableCardArePropsEqual.ts`) avec son test co-localisé — ne pas inline dans `React.memo()`.