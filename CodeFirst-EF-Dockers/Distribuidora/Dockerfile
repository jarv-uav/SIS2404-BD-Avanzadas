# syntax=docker/dockerfile:1

# ============================================================
# Etapa 1: build — restaura y publica con el SDK de .NET 10
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restaurar primero solo con el .csproj aprovecha la caché de capas:
# mientras no cambien los paquetes, `dotnet restore` no se vuelve a ejecutar.
COPY Distribuidora.csproj ./
RUN dotnet restore Distribuidora.csproj

COPY . .
RUN dotnet publish Distribuidora.csproj \
        --configuration $BUILD_CONFIGURATION \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# ============================================================
# Etapa 2: runtime — imagen mínima, sin SDK, usuario sin privilegios
# ============================================================
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

ENV DOTNET_ENVIRONMENT=Docker \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

COPY --from=build /app/publish .

# Las imágenes oficiales de .NET incluyen el usuario no-root "app" (UID en $APP_UID).
USER $APP_UID

ENTRYPOINT ["dotnet", "Distribuidora.dll"]
