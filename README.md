# SoClover

Implémentation multijoueur temps réel du jeu de société **« So Clover! »**.

Application full-stack : backend **ASP.NET Core 9.0**, frontend **React 18 / TypeScript**, communication temps réel via **SignalR**, persistance **PostgreSQL**, et **joueurs IA** (LLM).

> Projet *fan game* à but non commercial. Tous droits sur le jeu original « So Clover! » appartiennent à leurs auteurs et éditeurs respectifs.

**Version courante : `2.14.0`** — voir [`CHANGELOG.md`](CHANGELOG.md) pour l'historique complet.

## Fonctionnalités

- Parties multijoueurs en temps réel (SignalR) avec suivi des curseurs des joueurs.
- Boucle de jeu complète : Lobby → Écriture des indices → Devinette → Score.
- Joueurs IA pilotés par LLM (Anthropic en production, LM Studio en local).
- Dictionnaires Français et Anglais avec validation sémantique des indices.
- Code de partie lisible à 4 mots, partageable via l'URL `/g/<code>`.
- Compatibilité mobile / tactile.

## Stack technique

| Couche | Technologies |
|---|---|
| Backend | ASP.NET Core 9.0, EF Core, SignalR |
| Frontend | React 18, TypeScript, Vite, Zustand |
| Base de données | PostgreSQL (état de jeu en JSONB) |
| IA | Microsoft.Extensions.AI — Anthropic / LM Studio |
| Conteneurisation | Docker / Docker Compose |

## Démarrage rapide (développement local)

```bash
# Terminal 1 — Backend (watch mode)
dotnet watch --project SoClover

# Terminal 2 — Frontend (depuis SoClover/client/)
npm install
npm run dev   # Vite, proxy automatique vers localhost:5000
```

### Commandes utiles

```bash
# Backend
dotnet build                                        # Build de la solution
dotnet test                                         # Lancer tous les tests
dotnet test --filter "FullyQualifiedName~TestName"  # Test ciblé

# Frontend (depuis SoClover/client/)
npm run lint    # ESLint (strict)
npm run build   # Build de production
npm run test    # Tests front
```

### Docker

```bash
# Depuis SoClover/ — profil Production
docker compose --env-file .env build --no-cache web
docker compose --env-file .env up -d

# Profil Development (LM Studio) via override
docker compose -f compose.yaml -f compose.dev.yaml --env-file .env.dev up -d
```

## Architecture

```
SoClover/
├── Domain/           # Logique métier pure (Game = agrégat racine)
├── UseCases/         # Pattern Command/Handler
├── Infrastructure/   # EF Core, SignalR, dictionnaires, IA
├── RealTime/         # Hub SignalR (GameHub)
└── client/           # Frontend React/TypeScript
```

Pour les détails d'architecture, les conventions, la configuration et le fonctionnement des joueurs IA, voir [`CLAUDE.md`](CLAUDE.md) et [`docs/`](docs/).

## Configuration

La configuration suit le pattern multi-couches .NET :

- `appsettings.json` — défauts partagés non-secrets.
- `appsettings.{Environment}.json` — overrides non-secrets par environnement.
- `SoClover/.env` / `.env.dev` — **secrets uniquement** (jamais committés ; voir `.env.example`).

## Licence

Projet personnel non commercial. Voir la note *fan game* ci-dessus.
