# So Clover! - Projet Guide

Projet de jeu de société "So Clover!" (So Clover) implémenté en ASP.NET Core 9.0 avec SignalR pour le temps réel.

## Commandes de base

### Build & Run
- **Build de la solution** : `dotnet build`
- **Lancer l'application** : `dotnet run --project SoClover`
- **Lancer avec watch** : `dotnet watch --project SoClover`

### Tests
- **Exécuter tous les tests** : `dotnet test`
- **Exécuter un test spécifique** : `dotnet test --filter "FullyQualifiedName~TestName"`
- **Tests importants** : `SoClover.Tests/FullGameFlowTests.cs` (flux complet), `SoClover.Tests/BreakingGameTests.cs`.

### Base de données (EF Core)
L'application utilise PostgreSQL en production et un dépôt en mémoire en mode DEBUG.
- **Créer une migration** : `dotnet ef migrations add Name --project SoClover --startup-project SoClover`
- **Mettre à jour la base** : `dotnet ef database update --project SoClover`

## Architecture & Structure
- **SoClover/Domain** : Logique métier pure (Entités, Value Objects, Agrégats). Le `Game` est l'agrégat racine.
- **SoClover/UseCases** : Orchestration des actions utilisateur (Pattern Command/Handler).
- **SoClover/Infrastructure** : Implémentations techniques (Persistence EF Core, SignalR, Système de fichiers).
- **SoClover/RealTime** : Hubs SignalR pour la communication bidirectionnelle.
- **SoClover/wwwroot** : Frontend statique (HTML/JS/CSS). `app.js` gère la logique client.
- **SoClover.Tests** : Tests unitaires et d'intégration.

## Conventions de code
- **Langage** : C# 13.0 / .NET 9.0.
- **Style** : Suivre les standards .NET (PascalCase pour les méthodes/classes, _camelCase pour les champs privés).
- **Architecture** : Les UseCases doivent être isolés et injectés via leurs interfaces.
- **Domaine** : La logique de jeu doit résider autant que possible dans les classes du dossier `Domain`.
- **Persistance** : Le `EfGameRepository` stocke l'état complet du jeu sous forme de JSON (Document Store pattern).
- **Real-time** : Les événements du domaine sont publiés via `IEventPublisher` et diffusés par `SignalREventPublisher`.

## Technologies
- **Backend** : ASP.NET Core 9, SignalR, Entity Framework Core, Npgsql.
- **Frontend** : Vanilla JS, HTML5, CSS3, SignalR Client.
- **Infrastructure** : Docker, Docker Compose, Caddy.
