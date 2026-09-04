# Multi-stage Dockerfile for SoClover ASP.NET app

# ------------------------
# Build Client stage (React)
# ------------------------
FROM node:20-alpine AS build-client
WORKDIR /src/client
COPY SoClover/client/ ./
# VITE_DEBUG_MODE is baked at build time by Vite
# Usage: docker compose build --build-arg VITE_DEBUG_MODE=true web
ARG VITE_DEBUG_MODE=false
ENV VITE_DEBUG_MODE=$VITE_DEBUG_MODE
# --no-audit: npm 10's audit endpoint (/-/npm/v1/security/audits/quick) answers 503
# after a 120s timeout and npm retries it, adding ~415s to the 421s of this step for
# nothing. The pending request prints no output, so the build looks frozen on the
# previous stage's COPY. --no-fund silences the funding notice. Measured: 421s -> 6s.
RUN npm install --no-audit --no-fund
RUN npm run build

# ------------------------
# Build stage (.NET)
# ------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the main project file first for better layer caching
# NOTE: This repository contains a single web project (SoClover) with subfolders
# like Domain, Infrastructure, and UseCases inside it, not separate .csproj projects.
# We copy only the project file (no dependency on the .sln file).
COPY SoClover/SoClover.csproj SoClover/

# Restore
RUN dotnet restore SoClover/SoClover.csproj

# Copy the full source
COPY . .

# Copy the React build results to wwwroot
COPY --from=build-client /src/client/dist ./SoClover/wwwroot/

RUN dotnet publish SoClover/SoClover.csproj -c Release -o /app/publish --no-restore -p:DOCKER_BUILD=true

# ------------------------
# Runtime stage
# ------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Listen on port 8080 inside the container
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_ENVIRONMENT=Production

# curl pour healthcheck + utilisateur non-root pour la securite
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && addgroup --system --gid 1001 appgroup \
    && adduser --system --uid 1001 --ingroup appgroup appuser

# Copy published output
COPY --from=build /app/publish .

USER appuser

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -sf http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "SoClover.dll"]
