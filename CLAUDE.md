# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SoClover is a real-time multiplayer implementation of the "So Clover!" board game. Built with ASP.NET Core 9.0 backend, React 18 frontend, and SignalR for real-time communication.

## Versioning

Le projet suit **SemVer** (`vMAJOR.MINOR.PATCH`), avec des tags Git annotés et des GitHub Releases.
- **MAJOR** : changement structurel majeur (ex: réécriture du front-end).
- **MINOR** : nouvelle feature notable.
- **PATCH** : correctifs / durcissements sans nouvelle feature.

**Version courante : `2.19.0`.**

Jalons structurants : réécriture du front en React/TypeScript (v2.0), temps réel SignalR (v1.3), persistance PostgreSQL (v1.2), joueurs IA (v2.5), support du dictionnaire Anglais (v2.6), validation sémantique des indices étendue à l'Anglais (v2.7), code de partie lisible 4-mots exposé dans l'URL `/g/<code>` (v2.10), compatibilité mobile/tactile (v2.14), PWA installable plein écran iPhone (v2.15), régionalisation i18n EN/FR/PT (v2.16), UI unifiée single-layout laptop/mobile (v2.18), joueurs IA multilingues FR/EN/PT (v2.19). Historique complet des tags : voir [`CHANGELOG.md`](CHANGELOG.md).

### ⚠️ Pré-requis ABSOLU avant toute release : le code doit être sur `main`
**On ne tague et on ne release JAMAIS depuis une branche de feature.** Le déploiement
(`docs/deploy.md`) fait `git archive HEAD … SoClover/` sur la branche courante (= `main`) :
**tout ce qui n'est pas mergé dans `main` est invisible en prod**, même si un tag et une
GitHub Release existent. Le numéro de version (`APP_VERSION`) et le code peuvent alors
diverger — la prod affiche la bonne version mais sert l'ancien code (incident v2.11.0 :
feature taguée sur `feat/guessing-tried-placement-warning`, jamais mergée, jamais déployée ;
résolue en v2.12.0). Avant de taguer, vérifier impérativement :
- la feature est **mergée dans `main`** et `git status` est propre (rien d'oublié) ;
- le commit qu'on s'apprête à taguer est bien sur `main` : `git branch --contains <commit>` doit lister `main` ;
- `APP_VERSION` **et** le code de la feature sont tous deux présents sur `main` (pas seulement le bump de version) ;
- après build, vérifier que le bundle servi en prod contient bien la nouvelle `APP_VERSION` (cf. vérification post-déploiement).

### Processus à chaque nouvelle release (OBLIGATOIRE)
1. Mettre à jour `CONSTANTS.APP_VERSION` dans `SoClover/client/src/core/constants.ts` — c'est cette
   valeur qui s'affiche dans le footer de l'écran d'accueil (`components/home/HomeScreen.tsx`).
2. Mettre à jour la « Version courante » ci-dessus.
3. **Mettre à jour `CHANGELOG.md`** : ajouter une ligne en bas du tableau au format
   `| vX.Y.Z | \`<commit-court>\` | YYYY-MM-DD | <jalon : résumé concis de la feature/du correctif> |`.
   Le `<commit-court>` est le hash du commit de release (`git rev-parse --short HEAD`), la date est celle du jour.
   Si le jalon est structurant (réécriture, nouvelle brique majeure), l'ajouter aussi à la phrase de résumé
   « Jalons structurants » de la section Versioning ci-dessus.
   **Gotcha shell** : pour des release notes / contenu CHANGELOG contenant des backticks ou du code, écrire dans un
   fichier temporaire et le passer par fichier plutôt qu'en argument shell inline (évite l'interprétation des backticks).
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
docker compose -f compose.yaml -f compose.dev.yaml --env-file .env.dev build --no-cache
docker compose -f compose.yaml -f compose.dev.yaml --env-file .env.dev up -d
```
- `compose.yaml` est la base prod-ready. `compose.dev.yaml` est un override qui injecte `DOTNET_ENVIRONMENT=Development` et `LLM__BASEURL=http://host.docker.internal:1234/v1` pour parler à LM Studio sur l'hôte.
- Les secrets (PostgreSQL, `LLM__APIKEY`) viennent de `SoClover/.env` ou `.env.dev` (jamais committés).
- Afin de faciliter le build en local: déclencher directement .\local_build.ps1

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

- **Procédure complète** : `docs/ai-players/Operations_AI_Players.md` (setup, troubleshooting, A/B reasoning). Résultats de validation : `docs/ai-players/Epic_08_Validation_Results.md`.
- **Prompts** : format, placeholders & hot-reload dans `Infrastructure/AI/Prompts/README.md`.
- **Double contrôle de disponibilité** : la feature est active si `AIPlayers.Enabled` **ET** si la clé API du provider est acceptée. La seconde porte est `ILlmApiKeyProbe` (`CachedLlmApiKeyProbe`), qui mémoïse 5 min le verdict d'un `ILlmCredentialChecker`. Motivation : la console Anthropic permet de révoquer la clé à la demande, ce qui donne un interrupteur sans redéploiement, flag laissé à `true`.
  - **Verdict ternaire, pas booléen** : `Valid` / `Invalid` (401-403, preuve d'une révocation) / `Indeterminate` (timeout, 5xx, DNS). Seul un verdict conclusif est mis en cache, et un `Indeterminate` est **fail-open** — sans quoi un blip réseau grillerait la feature pour 5 minutes.
  - **Sonde Anthropic uniquement** : `AnthropicLlmCredentialChecker` appelle `GET /v1/models` (authentifié par `x-api-key`, **aucun token facturé**). Le provider OpenAI-compatible reçoit `AlwaysValidLlmCredentialChecker` — sonder LM Studio griserait le bouton tant que le serveur local n'est pas chargé, régression pénible en séance d'éval. Choix de l'impl dans `Program.cs`, en miroir du switch de `ChatClientFactory`.
  - **Limite connue** : détecte une clé **révoquée ou désactivée**, pas un **solde de crédits épuisé** — ce dernier laisse `/v1/models` répondre 200 et n'échoue que sur `/v1/messages`.
  - **Appel HTTP direct plutôt qu'Anthropic.SDK** : le verdict repose sur le code de statut exact, et un `HttpClient` s'injecte en test avec un handler factice.
- **Feature flag** : `AIPlayers.Enabled` dans `appsettings*.json`. Quand désactivé (ou clé refusée), endpoint `POST /api/games/{id}/ai-players` renvoie 403 — `CreateAIPlayer.Handler` consulte le flag **puis** la sonde, sans quoi un client resté ouvert au-delà du TTL ajouterait un bot qui échouerait en pleine génération d'indices. Frontend lit l'état via `GET /api/config` au boot (slice `appConfigSlice`) : `aiPlayersEnabled` est le booléen **effectif** (flag ET sonde) et `aiPlayersUnavailableReason` (`"disabled" | "apiKeyUnavailable" | null`) ne sert qu'à choisir le message de survol du bouton lobby.
- **Provider** : Development = LM Studio (`localhost:1234/v1`, `MaxConcurrency=1`), Production = Anthropic (`claude-haiku-4-5`, `MaxConcurrency=4`). Switch via `DOTNET_ENVIRONMENT`. Docker : utiliser `compose.dev.yaml` (injecte `LLM__BASEURL=http://host.docker.internal:1234/v1`). Binding via `ChatClientFactory`.
- **Secret** : `LLM__APIKEY` uniquement dans `.env` — jamais committé. Dev Anthropic : `dotnet user-secrets set "Llm:ApiKey" "sk-ant-..." --project SoClover`.
- **Champ `candidates`** : `AiClueDraft.Candidates` conserve le scratchpad structuré demandé par le
  prompt FR v5 (`"Mot (fort, faible)"`). Nullable et **non `[]` par défaut** — « champ absent » doit
  rester distinguable de « liste vide » — et volontairement **hors du `required`** du schéma JSON
  mono-clue, sinon le modèle n'aurait plus le droit de ne pas l'émettre. Aucun impact runtime : ce
  schéma n'est branché sur aucun `ChatOptions.ResponseFormat` à ce jour.
- **Structured logs** : log "AI clue LLM call completed" par appel (`LatencyMs`, `Provider`, `Model`, `PromptVersion`, `Attempt`, `RemainingDirections`) + log par clue (`IsValid`, `RejectionRules`). `PromptVersion` = champ `version:` du frontmatter du fichier prompt.
- **Mode reasoning** : flag `Llm.ReasoningEnabled` (défaut `false`). OFF = prompt prescriptif, JSON uniquement. ON = section `# REASONING` appendée au system prompt + paramètres natifs provider injectés via `IReasoningRequestConfigurator` (`ReasoningEffort` OpenAI, `ThinkingBudgetTokens` Anthropic). Certains modèles nécessitent un system prompt trigger (`Llm.ReasoningSystemPromptPathEnabler`) pour activer leur reasoning natif.
- **Mode de génération** : flag `Llm.GenerationMode` (défaut `PerBoard`, surcharge `LLM__GENERATIONMODE`). `PerBoard` = 1 appel LLM par board couvrant les 4 directions restantes (pipeline historique). `PerDirection` = 1 appel par direction (jusqu'à 4 appels séquentiels par board). Sélection câblée dans `Program.cs` via `ActivatorUtilities.CreateInstance` selon `IOptions<LlmOptions>.GenerationMode` → résout `GenerateAIClues.Handler` ou `GenerateAICluesPerDirection.Handler`. Motivation : `PerDirection` fiabilise la convergence des modèles reasoning locaux (ministral 14B) qui n'émettaient pas le JSON final en `PerBoard`.
- **Matrice 2×2 (granularité × reasoning)** : les axes `GenerationMode` et `ReasoningEnabled` sont **orthogonaux** — les 4 combinaisons sont valides et indépendantes (`PerDirection` + reasoning OFF est jugé prometteur côté qualité/coût). Le découplage est garanti par la factorisation dans `AiCluesGeneratorBase.CallLlmAsync` (lit `ReasoningEnabled` quel que soit le pipeline appelant).
- **Coût & latence PerDirection** : worst-case **4×(MaxRetries+1)** appels par board (avec `maxRetries: 0` reco reasoning → exactement 4 appels). Consomme donc `maxCallsPerGame` plus vite que `PerBoard`. Exécution **séquentielle** (pas de mutation concurrente de `Game`) → latence totale = somme des appels ; en reasoning mode le total peut être lourd, mais chaque appel single-direction reste plus court à converger qu'un appel multi-directions.
- **Langues supportées par les joueurs IA** : **FR, EN et PT** — les trois dictionnaires livrés. Ajouter une langue = 3 fichiers prompt + un provider + une branche `AiCluePromptProviderFactory` + **un validateur d'indices** ; recette complète dans `Infrastructure/AI/Prompts/README.md`. Le validateur n'est pas optionnel : sans lui `ClueValidatorFactory` retombe sur `NullClueValidator`, aucun indice IA n'est jamais rejeté et la boucle `retryFeedback` ne se déclenche jamais.
- **Une seule liste de langues côté back** : `SemanticValidationSupport.ByPrefix` porte la table préfixe → validateur, consultée par `ClueValidatorFactory` **et** par `Game.SemanticClueCheckEnabled`. Les préfixes sont comparés à `TextNormalizer.Normalize`, qui retire les diacritiques — d'où `portugues`, qui couvre `Portuguese_(from_FR_OFF)` comme `Português`. Le miroir front `client/src/core/clueValidation.ts` reste une duplication assumée (frontière de langage) et se met à jour dans le même commit.
- **Prompts AI Clues** : trois fichiers co-localisés par langue dans `SoClover/Infrastructure/AI/Prompts/<lang>/` :
  - `board-clues.md` — utilisé par le pipeline `PerBoard` (multi-directions, JSON `{ clues: [...] }`).
  - `board-clues-per-direction.md` — utilisé par le pipeline `PerDirection` (mono-cible, JSON `{ direction, candidates, explanation, clueWord }`). C'est **le prompt réellement servi en prod** (`generationMode: PerDirection` dans `appsettings.Production.json` comme `.Development.json`).
  - `board-clues-per-direction.reasoning.md` — **variante reasoning-only** du pipeline `PerDirection`, co-localisée par langue. Chargée **uniquement** quand `Llm.ReasoningEnabled=true` ET `Llm.GenerationMode=PerDirection` ET que le provider de langue injecte un path non-null (FR, EN et PT aujourd'hui). Le fichier **EST** la variante reasoning : aucune section `# REASONING` n'y est appendée (une section `# REASONING` présente serait ignorée). Sections requises : `# SYSTEM`, `# USER` (placeholders `{{boardLayout}}`, `{{directionToResolve}}`, `{{allBoardWordsList}}`, `{{retryFeedback}}`), `# RETRY_FEEDBACK` (`{{rejectedAttemptsByDirection}}`). Politique **fail-fast** : si le path est injecté mais le fichier absent du disque, `BuildSingleDirectionCluePrompt` throw `FileNotFoundException`. Convention **opt-in** pour les langues futures (path `null` → voie legacy : charge `board-clues-per-direction.md` et appende `# REASONING`). Le `PromptVersion` du log « AI clue LLM call completed » reflète le `version:` du fichier chargé — utile pour A/B reasoning vs non-reasoning.
  Convention : **le pipeline détermine le prompt** (jamais déduit du `remaining.Count`) → pas de fuite cross-mode lors d'un retry partiel PerBoard. La traçabilité est dans `PromptVersion` du log structuré « AI clue LLM call completed ».
- **`version:` = génération de contenu partagée entre langues**, pas l'historique d'un fichier : FR, EN et PT portent le même numéro tant qu'ils portent les mêmes règles (aujourd'hui `board-clues.md` v10, `board-clues-per-direction.md` v5, `.reasoning.md` v1). C'est ce qui rend `PromptVersion` comparable entre langues dans les logs et dans `eval/LEDGER.md`. **Toucher au contenu d'un prompt ⟹ bumper son `version:` dans le même geste** ; une langue peut sauter des numéros en rattrapant une autre (EN est passé de v1 à v5), jamais réutiliser un numéro pour un contenu différent. `SoClover.Tests/Ai/PackagedPromptGenerationTests.cs` épingle la génération de chaque famille pour chaque langue — une édition sans bump devient un test rouge.
- **Traduire un prompt, ce n'est pas le transposer** : les relations qui reposent sur la langue elle-même (11 polysémie, 12 spécialisation croisée, passe « langue et jeu de mots », exemple de racine de la règle absolue 3) n'ont pas d'équivalent littéral. On choisit des équivalents natifs de même propriété et on consigne la table de substitution dans la section `# NOTES` du fichier (ignorée par `FilePromptLoader`) — voir `en/` et `pt/`. Le PT vise le **registre brésilien**, cohérent avec `Portuguese_(from_FR_OFF).txt`.

### Harnais d'évaluation des indices IA (`SoClover.Eval/`)

> **⚠️ Toute phase de test de LLM pour les joueurs IA passe par la skill `soclover-eval`.**
> Évaluer, comparer, calibrer ou régler un modèle — générateur comme décodeur —, lancer un verbe du
> harnais, charger un modèle dans LM Studio pour un test, toucher aux prompts d'indices ou au prompt
> décodeur, lire ou écrire une ligne de `eval/LEDGER.md`, interpréter un `recovery` / un accord /
> un κ / les quatre portes, ou modifier le code de `SoClover.Eval` : **invoquer la skill d'abord**,
> avant toute commande et avant toute question de clarification. Elle porte le protocole, les gardes
> méthodologiques et les pièges d'artefacts qui ne sont plus répétés ici.

- **Projet console hors ligne, jamais déployé.** Le `Dockerfile` ne restaure que
  `SoClover/SoClover.csproj` et `docs/deploy.md` fait `git archive HEAD … SoClover/` : ne jamais
  y ajouter `SoClover.Eval`. Le projet **est** en revanche dans `SoClover.sln` — `dotnet build` et
  `dotnet test` le couvrent (ses suites vivent dans `SoClover.Tests/Eval/`), la défense en
  profondeur passe par `.dockerignore` et `export-ignore`.
- **Où est quoi** : spécifications dans `Specs/AI_Clue_Eval_Loop/` (PRD + designs P0-P7), mode
  d'emploi des verbes dans `SoClover.Eval/README.md`, mesures dans `eval/LEDGER.md` (append-only,
  **jamais réécrit**), protocole et gardes dans la skill `soclover-eval`.
- **Artefacts committés, à ne pas régénérer** : `eval/boards.dev.jsonl` (40 boards) et
  `eval/boards.test.jsonl` (60) portent leur seed et leur hash — un banc qui bouge invalide tout
  l'historique du registre. `eval/human/` est committé (corpus humain irremplaçable) ;
  `eval/runs/` est gitignoré.
- **Briques partagées avec la prod**, à ne pas dupliquer côté éval : `Domain/BoardGeometry.cs`,
  `Domain/ClueAcceptance.cs`, `Infrastructure/AI/AiClueLlmCaller.cs`,
  `Infrastructure/AI/AiClueResponseParser.cs`, `Infrastructure/AI/LlmCallExceptions.cs`.
  `AiClueLlmCaller` **ne journalise pas** : il rend latence / version de prompt / modèle effectif /
  usage, et `AiCluesGeneratorBase` conserve ses messages de log inchangés.
- **Langfuse (local, `tools/langfuse/`) est la source des prompts du harnais** : `generate`, `decode`
  et `calibrate` résolvent `generator-fr-per-direction`, `decoder-fr-clue`, `decoder-fr-board` par
  label (défaut `production`) et les matérialisent sous `eval/prompts/resolved/<sha12>/fr/` — même
  chemin canonique, donc **même empreinte de décodeur** qu'avec le fichier. Aucun repli silencieux :
  `--prompt-source file` s'écrit. La prod lit toujours ses fichiers ; `langfuse-pull` y ramène une
  version retenue. Rien de Langfuse n'entre dans `SoClover/`.

## Testing

Key test files:
- `FullGameFlowTests.cs` - Complete happy path through all phases
- `BreakingGameTests.cs` - Edge cases and error handling
- `DomainRotationTests.cs` - Card rotation logic

Tests use `TestClock` for time control and `InMemoryGameRepository` for isolation.

- **Déterminisme du tirage de mots** : les suites IA et `ClueExplanationVisibilityTests` sont câblées sur `DeterministicWordDictionary` (`SoClover.Tests/Helpers/`), pas sur `FileWordDictionary`. Motif : les mots de carte sont tirés au hasard (`WordsPool.DrawWords` instancie un `Random` non seedé à chaque tirage) et le vrai dictionnaire ne garantit pas les propriétés dont ces tests dépendent — la racine R2 de « Botte » (« bott ») est une sous-chaîne de `admin-bottom`, ce qui fait rejeter l'indice littéral et laisse le board incomplet. Tout test qui pose un indice codé en dur, ou qui attend qu'un mot du board soit rejeté comme indice, doit utiliser ce dictionnaire — et déclarer son littéral dans `DeterministicWordDictionaryTests.ClueLiteralsUsedByTests`. Les suites qui exercent volontairement le vrai dictionnaire (`DictionaryIntegrityTests`, `SetClueWithValidationTests`, `CreateGameCodeTests`, `WordsPoolPersistenceTests`) restent sur `FileWordDictionary`.
- **Avant de passer à la suite** : après chaque tâche/commit, lancer toute la suite de tests et vérifier un build propre (`dotnet test`, et côté front `npm run lint && npm run build && npm run test`).

## Configuration

**Règle directrice — qui met quoi** (pattern .NET idiomatique multi-couches) :
- `appsettings.json` : défauts partagés, non-secrets, communs à tous les environnements (`GameDefaults`, tuning `Llm` : `defaultTemperature`, `topP`, `maxOutputTokens`, `maxRetries`, `timeoutSeconds`, `maxCallsPerGame`, `generationMode`). `topP` et `maxOutputTokens` sont nullables (null = défaut du provider) et appliqués sur le `ChatOptions` de chaque appel (`GenerateAIClues.CallLlmAsync`). `defaultTemperature` sert de défaut quand le joueur IA n'a pas de température explicite dans son `AIConfig`. Pour les modèles reasoning Mistral, reco : `temp 1.0 / topP 0.95` + `maxRetries 0` (un run reasoning échoué coûte cher, inutile de retenter ×3).
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
- Lors d'une phase d'implémentation de plan, toujours livrer en plusieurs commits atomiques plutôt qu'un unique gros commit. Toujours implémenter en TDD.

### Git & hygiène des fichiers

- **Casse sensible** : les chemins et identifiants Git sont sensibles à la casse sur ce projet (utiliser `AI`, pas `Ai`). Attention à l'insensibilité à la casse de Windows lors du staging / renommage.
- **Fichiers redondants / résiduels** : l'action par défaut est la **suppression** — ne pas les stager ni les corriger sauf demande explicite.

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
- **Deux seuils de longueur dans `SubstringClueValidator`** : `MinWordLength = 3` s'applique à
  l'**indice** (garde contre les sous-chaînes triviales : « bo » dans « bondir ») et sert de
  longueur minimale de racine aux heuristiques morphologiques ; `MinBoardWordLength = 2` s'applique
  aux **mots de carte**, volontairement plus bas — « Or », « Os », « Nu » sont de vrais mots FR, et
  les ignorer laissait un joueur donner comme indice un mot présent sur son propre plateau.
- **`Game.SetClueWithValidation` ne valide plus lui-même** : la séquence trim → plafond de longueur
  → `ClueText` → validateur vit dans `Domain/ClueAcceptance.cs`, appelée sans `Game` par le harnais
  d'évaluation. Toute évolution de la règle d'acceptation se fait là, pas dans `Game`.
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