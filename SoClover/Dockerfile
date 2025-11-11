# Multi-stage Dockerfile for SoClover ASP.NET app

# ------------------------
# Build stage
# ------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and the main project files first for better layer caching
# NOTE: This repository contains a single web project (SoClover) with subfolders
# like Domain, Infrastructure, and UseCases inside it, not separate .csproj projects.
# So we only copy the SoClover project file (and optionally the solution).
COPY SoClover/SoClover.csproj SoClover/
COPY SoClover.sln ./

# Restore
RUN dotnet restore SoClover/SoClover.csproj

# Copy the full source and publish Release
COPY . .
RUN dotnet publish SoClover/SoClover.csproj -c Release -o /app/publish --no-restore

# ------------------------
# Runtime stage
# ------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Listen on port 8080 inside the container
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_ENVIRONMENT=Production

# Copy published output
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "SoClover.dll"]
