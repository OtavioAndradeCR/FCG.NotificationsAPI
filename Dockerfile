# ──────────────────────────────────────────────
# Estágio 1: Build
# ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY src/FCG.NotificationsAPI/FCG.NotificationsAPI.csproj ./src/FCG.NotificationsAPI/
COPY src/FCG.Shared/FCG.Shared.csproj ./src/FCG.Shared/
RUN dotnet restore ./src/FCG.NotificationsAPI/FCG.NotificationsAPI.csproj

COPY src/ ./src/
RUN dotnet publish ./src/FCG.NotificationsAPI/FCG.NotificationsAPI.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ──────────────────────────────────────────────
# Estágio 2: Runtime Lambda
# ──────────────────────────────────────────────
FROM public.ecr.aws/lambda/dotnet:10 AS runtime
WORKDIR ${LAMBDA_TASK_ROOT}

COPY --from=build /app/publish .

CMD ["FCG.NotificationsAPI::FCG.NotificationsAPI.Functions.NotificationsFunction::HandleAsync"]
