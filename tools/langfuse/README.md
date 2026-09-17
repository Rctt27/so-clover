# Langfuse v4 local — harnais d'évaluation SoClover.Eval

Stack Langfuse auto-hébergée, exécutée uniquement en local pour servir de source de prompts et de
store d'expériences à `SoClover.Eval`. **Jamais déployée** : ce répertoire n'est référencé par
aucun `Dockerfile` ni `docs/deploy.md` de production, et `SoClover.Eval` lui-même n'est jamais
poussé en prod (voir `CLAUDE.md`, section « Harnais d'évaluation des indices IA »).

## Démarrage

```bash
cd tools/langfuse
cp .env.example .env
# éditer .env : générer chaque secret avec `openssl rand -hex 32`
# (ENCRYPTION_KEY doit faire exactement 64 caractères hex)
cd ../..
docker compose --project-directory tools/langfuse --env-file tools/langfuse/.env up -d
```

Vérifier la santé :

```bash
curl -s http://localhost:3000/api/public/health
# {"status":"OK","version":"4.36.1"}
```

Se connecter sur http://localhost:3000 avec l'utilisateur défini par `LANGFUSE_INIT_USER_EMAIL` /
`LANGFUSE_INIT_USER_PASSWORD` dans `.env`. Le projet « SoClover Eval » (org `soclover`, projet
`soclover-eval`) est créé automatiquement au premier démarrage via les variables
`LANGFUSE_INIT_*` — pas de clic manuel dans l'UI. Les clés API du projet sont celles fixées dans
`.env` : `LANGFUSE_INIT_PROJECT_PUBLIC_KEY` (`pk-lf-soclover-local`) et
`LANGFUSE_INIT_PROJECT_SECRET_KEY` (`sk-lf-…`).

Vérifier que les clés fonctionnent (doit renvoyer du JSON 200, pas 401) :

```bash
curl -s -u "pk-lf-soclover-local:<sk-lf-...>" "http://localhost:3000/api/public/v2/prompts"
```

## Arrêt

```bash
# Arrête les conteneurs, conserve les volumes (données persistées)
docker compose --project-directory tools/langfuse --env-file tools/langfuse/.env down

# Arrête et efface tout (postgres, clickhouse, minio, redis) — repart de zéro
docker compose --project-directory tools/langfuse --env-file tools/langfuse/.env down -v
```

## Version épinglée et montée de version

Tag actuellement épinglé : **v4.36.1** (le plus récent `v4.x.y` au moment de l'écriture, via
`gh release list -R langfuse/langfuse --limit 10` — si `gh` échoue, utiliser
`curl -s https://api.github.com/repos/langfuse/langfuse/releases?per_page=10`).

Les images `docker.langfuse.com/langfuse/langfuse` et `docker.langfuse.com/langfuse/langfuse-worker`
sont épinglées au tag exact (pas de `:4` ni `:latest`) dans `docker-compose.yml`.

Pour monter de version :
1. Choisir le nouveau tag `vX.Y.Z`.
2. Retélécharger le compose officiel :
   `curl -fsSL -o /tmp/langfuse-compose.yml https://raw.githubusercontent.com/langfuse/langfuse/vX.Y.Z/docker-compose.yml`
3. Rejouer manuellement sur ce nouveau fichier les mêmes substitutions que celles de ce
   `docker-compose.yml` : remplacer `:4`/`:latest` par `:X.Y.Z`, supprimer les valeurs par défaut de
   chaque variable marquée `# CHANGEME` (au profit de `${NOM_DE_LA_VARIABLE}` sans défaut), et
   relier les identifiants MinIO des uploads S3 à `${MINIO_ROOT_PASSWORD}` plutôt que d'introduire
   de nouvelles variables.
4. `docker compose --project-directory tools/langfuse --env-file tools/langfuse/.env up -d` puis
   revérifier la santé et l'appel authentifié ci-dessus.

## Où trouver les clés / secrets

Tout est dans `tools/langfuse/.env` (gitignoré, jamais committé — voir `.gitignore`). Un
`.env.example` documente chaque variable avec une valeur factice. Le client C# de
`SoClover.Eval` lit `pk-lf-soclover-local` / `sk-lf-…` depuis
`SoClover.Eval/evalsettings.local.json` (également gitignoré).

## Ports

Tous les ports par défaut du compose officiel v4.36.1 étaient libres sur cette machine au moment
du démarrage (3000, 3030, 5432, 6379, 8123, 9000, 9090, 9091) — **aucun remap n'a été nécessaire**.
Si un conflit apparaît un jour (ex. un Postgres local de SoClover sur 5432), remapper le port hôte
concerné dans `docker-compose.yml`, par exemple `127.0.0.1:5433:5432` pour `postgres`, et documenter
le remap ici.

## Mode "events_only" (v4.36.1) — endpoints de lecture indisponibles

Cette instance tourne en mode `events_only`. Plusieurs endpoints de **lecture** REST y répondent
404 (`"This endpoint is not available on deployments running in Langfuse v4 events_only mode"`) :
`GET /api/public/traces/{id}`, la liste des traces, `GET /api/public/observations` (v1),
`GET /api/public/datasets/{name}/runs/{run}` et `GET /api/public/scores/{id}`. Deux conséquences
pour `SoClover.Eval` :

- l'id d'une experiment se retrouve via `GET /api/public/experiments?fromStartTime=…&toStartTime=…`
  filtré côté client par `name`, pas via un endpoint `runs/{runName}` (absent sur ce déploiement) ;
- un score publié (`POST /api/public/scores`) ne se relit **pas** par API — son rattachement se
  vérifie visuellement dans l'UI (Datasets → `<nom du dataset>` → Experiments), jamais par un
  `curl` de contrôle.

`GET /api/public/v2/observations?traceId=…` reste disponible et suffit à vérifier qu'un span a
bien été ingéré.

## Cohabitation mémoire avec LM Studio (spec §12)

Mesure effectuée avec la stack Langfuse seule démarrée (6 conteneurs), **sans** modèle chargé dans
LM Studio (limitation de l'environnement d'exécution de cette tâche — pas d'accès à l'UI LM Studio
pour charger `qwen/qwen3-8b`) :

- Mémoire cumulée des 6 conteneurs (`docker stats --no-stream`) : **≈ 2.34 GiB**
  (langfuse-web 1.23 GiB, langfuse-worker 0.69 GiB, clickhouse 0.26 GiB, postgres 0.09 GiB,
  minio 0.06 GiB, redis 0.005 GiB).
- RAM libre Windows (`Get-CimInstance Win32_OperatingSystem`) à ce moment : **≈ 1.18 GiB libre**
  sur ≈ 31 GiB au total.

**Mesure restante à faire par l'auteur** : charger `qwen/qwen3-8b` dans LM Studio pendant que la
stack Langfuse tourne, puis relancer `docker stats --no-stream` et vérifier la RAM libre Windows.
Le seuil de repli de la spec (§12) est **moins de 4 Gio libres avec les deux modèles du harnais
chargés** — la mesure ci-dessus, prise *sans* LM Studio, montre déjà une marge faible (~1.18 GiB
libre) sur cette machine ; il est probable que le seuil de repli soit atteint ou dépassé une fois
un modèle 8B chargé dans LM Studio en plus de la stack Langfuse. À confirmer avant de compter sur
l'exécution simultanée des deux systèmes en développement quotidien.

## Rappel

`tools/langfuse/` et tout ce qu'il contient (y compris ce README) documentent un outillage de
développement local pour `SoClover.Eval`. **Rien ici n'est jamais déployé** : pas de référence
dans `Dockerfile`, `docs/deploy.md`, ni aucun pipeline de production SoClover.
