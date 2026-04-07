# Multi-stage Dockerfile for SoClover ASP.NET app

  # ------------------------                                                                                                                                                                                                                                                                                                               
  # Build Client stage (React)                                                                                                                                                                                                                                                                                                             
  # ------------------------                                                                                                                                                                                                                                                                                                               
  FROM node:20-alpine AS build-client                                                                                                                                                                                                                                                                                                      
  WORKDIR /src/client                                                                                                                                                                                                                                                                                                                      
  COPY SoClover/client/ ./                                                                                                                                                                                                                                                                                                                 
  RUN npm install                                                                                                                                                                                                                                                                                                                          
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

# Clean existing wwwroot except dictionaries (which are needed by the backend)
# and move legacy-vanilla to a backup or just remove it if we want to be sure it's not served.
RUN rm -rf SoClover/wwwroot/legacy-vanilla SoClover/wwwroot/MouseTracking SoClover/wwwroot/index.html

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

# curl pour HEALTHCHECK + utilisateur non-root pour la sécurité
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
