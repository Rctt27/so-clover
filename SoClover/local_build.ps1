# Build local SoClover (profil Development) et nettoyage des images orphelines
Set-Location $PSScriptRoot

docker compose -f compose.yaml -f compose.dev.yaml --env-file .env.dev build --no-cache
if (-not $?) { exit 1 }

docker compose -f compose.yaml -f compose.dev.yaml --env-file .env.dev up -d
if (-not $?) { exit 1 }

docker system prune -f
