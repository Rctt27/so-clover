# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SoClover is a real-time multiplayer implementation of the "So Clover!" board game. Built with ASP.NET Core 9.0 backend, React 18 frontend, and SignalR for real-time communication.

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
docker compose --env-file .env build --no-cache web                                                                                                                                                                                                                                                                                                                                                                  
docker compose --env-file .env up -d 
```
- Pas de reverse proxy dans compose (Caddy supprimé) — l'app écoute directement sur le port exposé.
- Les secrets PostgreSQL viennent de `SoClover/.env` (ne pas committer `.env`).

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

- **Provider switch** : dev local → `appsettings.Development.json` (`Provider=OpenAI`, `BaseUrl=http://localhost:1234/v1`, `MaxConcurrency=1`). Prod → `appsettings.Production.json` (`Provider=Anthropic`, `DefaultModel=claude-haiku-4-5`, `MaxConcurrency=4`). La seule différence runtime est le binding de `IChatClient` via `ChatClientFactory` (`SoClover/Infrastructure/AI/ChatClientFactory.cs`).
- **Secret Anthropic** : en prod via env var `LLM__APIKEY` (cf. `SoClover/.env`). En dev pour tester Anthropic : `dotnet user-secrets set "Llm:ApiKey" "sk-ant-..." --project SoClover`. Ne **jamais** committer la clé.
- **LM Studio** : application desktop, expose un serveur OpenAI-compatible sur `http://localhost:1234/v1` (port configurable dans l'UI). L'`ApiKey` n'est pas vérifiée — `"lm-studio"` factice suffit mais doit être non-vide (`LlmOptionsValidator`).
- **Structured logs** : chaque appel LLM produit 1 log "AI clue LLM call completed" (`LatencyMs`, `Provider`, `Model`, `PromptVersion`, `Attempt`, `RemainingDirections`). Chaque clue validée/rejetée produit 1 log avec `IsValid` + `RejectionRules`. Utiliser ces props pour comparer 2 versions de prompt (`PromptVersion` = champ `version:` du frontmatter de `Infrastructure/AI/Prompts/<lang>/*.md`).
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

## Configuration

- DEBUG mode uses in-memory repository
- RELEASE mode uses PostgreSQL (`DATABASE_URL` or `ConnectionStrings:GameDb`)
- Word dictionaries in `Infrastructure/Dictionaries/` (co-localisés avec `FileWordDictionary`)
- Game settings in `appsettings.json` → section `GameDefaults` (via `IOptions<GameDefaultsOptions>`)
- Env vars centralisées dans `SoClover/.env` (PostgreSQL + VITE_*) — template : `SoClover/.env.example`
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

- **Scoring endpoint double-mapping** : `GetScoring.cs` retourne un `BoardResultDto`, mais `/api/games/{id}/scoring` dans `Program.cs` le re-mappe manuellement en objet anonyme. Ajouter un champ au DTO exige de mettre à jour les **deux** fichiers.
- **Dépendance UseCase → RealTime interdite** : Ne jamais référencer `GameHub` directement depuis un UseCase. Utiliser une interface injectable (ex. `IConnectionTracker` dans `SoClover/RealTime/`) avec injection optionnelle (`= null`) — les tests passent sans l'enregistrer, le runtime injecte l'implémentation réelle.
- **`ActivePlayers` vs `Players`** : `game.ActivePlayers` exclut les joueurs déconnectés (`IsDisconnected = true`). Toute logique de flux (SubmitBoard, StartGuessingPhase, MoveToNextBoard, MoveToNextGuessingBoard) doit utiliser `ActivePlayers`. `game.Players` reste pour le scoring et l'affichage complet.

### Frontend – Design & Assets

- **Avant tout travail visuel**, explorer `SoClover/client/src/assets/styles/` pour réutiliser les variables CSS et styles existants.
- Les couleurs, espacements, animations et autres tokens visuels doivent être centralisés dans `assets/styles/` — jamais hardcodés inline dans les composants.
- Si une valeur visuelle (ex: palette de confettis, timing d'animation) n'existe pas encore dans les styles centralisés, la créer dans le fichier approprié de `assets/styles/` avant de l'utiliser dans le composant.
- Réutiliser les assets existants (`public/sounds/`, `public/images/`) plutôt que d'en embarquer de nouveaux sans vérification préalable.