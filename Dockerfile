# ──────────────────────────────────────────────
# Estágio 1: Build
# ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/FCG.NotificationsAPI/FCG.NotificationsAPI.csproj


RUN dotnet publish \
    src/FCG.NotificationsAPI/FCG.NotificationsAPI.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ──────────────────────────────────────────────
# Estágio 2: Runtime
# ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FCG.NotificationsAPI.dll"]
