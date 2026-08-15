# Northern Link API — the one shared gateway composing every domain library.
#
# BUILD CONTEXT IS THE REPOSITORY ROOT, not Backend/:
#   docker build -f Backend/Dockerfile -t northernlink-api .
# Directory.Build.props and Directory.Packages.props (net10.0, warnings-as-errors, central
# package versions) live at the workspace root, one level above Backend/, and MSBuild walks
# upward to find them. A build context of Backend/ silently loses both.

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Platform-wide build settings first: they change far less often than source, so this layer
# stays cached across ordinary code edits.
COPY Directory.Build.props Directory.Packages.props ./
COPY Backend/ Backend/

# Run from Backend/ so Backend/global.json's SDK pin (10.0.100, rollForward latestFeature) is
# discovered — global.json resolution starts at the working directory, not the project file.
WORKDIR /src/Backend

# Publish the API project alone rather than the solution: that builds Shared plus the ten
# domain libraries and nothing else, skipping all 15 test projects. Never build from the repo
# root or the whole solution — AppHost/ is a local-dev orchestrator whose AppHost.cs is
# gitignored and therefore absent in CI.
RUN dotnet publish src/Api/NorthernLink.Api/NorthernLink.Api.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

# No port is pinned here and no EXPOSE is declared. This API is cluster-internal — it is
# reached only by the Next.js servers' /api/* proxy, never from a browser — so which port it
# listens on is a deployment decision, set with ASPNETCORE_HTTP_PORTS from the ConfigMap
# (k8s/base/api-config.yaml). The aspnet base image already defaults to 8080, so the image
# still runs standalone with no configuration at all.

# No HEALTHCHECK instruction either: /health (readiness) and /alive (liveness) are mapped
# unconditionally by MapDefaultHostEndpoints, and the orchestrator's probes own them. A Docker
# HEALTHCHECK here would duplicate that and mean nothing in Kubernetes.

# The aspnet image already defines a non-root `app` user (UID 1654).
USER app

ENTRYPOINT ["dotnet", "NorthernLink.Api.dll"]
