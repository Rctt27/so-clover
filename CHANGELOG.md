# Changelog

Historique des versions de SoClover. Le projet suit [SemVer](https://semver.org/lang/fr/) (`vMAJOR.MINOR.PATCH`) avec des tags Git annotés et des GitHub Releases.

| Tag | Commit | Date | Jalon |
|-----|--------|------|-------|
| v1.0.0 | `0b8e59f` | 2025-10-31 | Première version jouable de bout en bout (front legacy vanilla JS) |
| v1.1.0 | `b0a235b` | 2025-11-01 | Phases minutées + paramètres de partie dynamiques |
| v1.2.0 | `f2ffadf` | 2025-11-11 | Persistance PostgreSQL (EF Core) + déploiement Docker |
| v1.3.0 | `8909f99` | 2025-12-05 | Temps réel via SignalR (fin du polling) |
| v1.4.0 | `1124e7b` | 2025-12-21 | Suivi des curseurs + UX clavier |
| v2.0.0 | `1e333c6` | 2026-01-22 | Réécriture complète du front en React/TypeScript |
| v2.1.0 | `152594d` | 2026-04-07 | Gamification : sons, confettis, transitions, musique |
| v2.2.0 | `cb3317f` | 2026-04-09 | Robustesse : mode debug, centralisation config, cleanup legacy |
| v2.3.0 | `954b945` | 2026-04-15 | Déconnexion/reconnexion + kick admin |
| v2.4.0 | `c7d4189` | 2026-04-18 | Validation sémantique des indices (FR) |
| v2.5.0 | `af44b5b` | 2026-05-21 | Joueurs IA (LLM Anthropic / LM Studio) |
| v2.5.1 | `a72dd88` | 2026-05-26 | Durcissement IA prod + affichage de la version dans l'UI |
| v2.5.2 | `d814fef` | 2026-05-26 | Release : APP_VERSION mise à jour |
| v2.6.0 | `3d7462d` | 2026-05-27 | Joueurs IA disponibles aussi pour le dictionnaire Anglais (EN) |
| v2.7.0 | `526f43b` | 2026-05-27 | Validation sémantique des indices étendue au dictionnaire Anglais (parité IA + humains) |
| v2.8.0 | `7c6af91` | 2026-05-27 | Mode reasoning natif togglable pour les joueurs IA (LM Studio Ministral-Reasoning, OpenAI o-series, Anthropic extended thinking) |
| v2.9.0 | `85446e5` | 2026-05-28 | Mode de génération IA PerDirection — 1 appel LLM par direction (fiabilise la convergence des modèles reasoning locaux) |
| v2.9.1 | `d9c1d38` | 2026-05-29 | Prompt reasoning-only dédié PerDirection (FR+EN) — fichier `board-clues-per-direction.reasoning.md` chargé quand reasoning ON, sans procédure prescriptive parasite |
| v2.9.2 | `b4393a8` | 2026-05-29 | Progression de génération des indices IA sur l'écran d'attente — indices validés (X/4) + retries par direction, via event SignalR `AiClueProgressUpdate` (couvre PerBoard & PerDirection) |
| v2.10.0 | `bdcfdce` | 2026-05-31 | Code de partie lisible 4-mots anglais slugifiés (`lamp-pear-house-sheep`) remplaçant le GUID — PK PostgreSQL en `text`, code exposé dans l'URL `/g/<code>` (History API) pour retour/partage de partie |
| v2.10.1 | `5bc49f5` | 2026-05-31 | Isolation de la vérification sémantique des indices à la phase WritingClues — suppression des faux positifs en phase Guessing causés par la 5e carte leurre |
| v2.11.0 | `cc4cd3b` | 2026-06-01 | ⚠️ Tag posé sur une branche (`feat/guessing-tried-placement-warning`) jamais mergée dans `main` : la feature « position déjà essayée » décrite n'a jamais été déployée sous cette version. Livrée effectivement en **v2.12.0**. |
| v2.11.1 | `16516c9` | 2026-06-01 | Release : APP_VERSION mise à jour (commit de métadonnées, aucun code de feature embarqué — cf. note v2.11.0) |
| v2.12.0 | `8478cc8` | 2026-06-01 | Avertissement « position déjà essayée » en phase Guessing — outline orange + icône warning sur les combinaisons (carte/position/rotation) déjà tentées et fausses (historique autoritatif côté `Game`), bouton « Valider » grisé tant qu'une carte est en position connue fausse. Livraison effective de la feature prématurément taggée v2.11.0 (jamais mergée dans `main`). |
| v2.12.1 | `ee60713` | 2026-06-02 | Layout du plateau height-aware (Writing + Guessing) — plateau dimensionné au plus grand carré tenant en largeur ET hauteur via container queries (`clamp(min, min(100cqw,100cqh), max)`), boutons d'action toujours visibles sans scroll. Optimisations tablette (détection `pointer:coarse`) : rotation compacte autour de « Valider », plateau maximisé, avertissement « position déjà essayée » déplacé en infobulle (suppression du reflow). |
