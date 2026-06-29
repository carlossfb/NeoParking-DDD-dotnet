# ─── stage 1: restore & build ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo

# copia solution e csprojs de produção para cache de dependências
COPY Neoparking.sln .
COPY src/Shared/NeoParking.Shared.Kernel/NeoParking.Shared.Kernel.csproj   src/Shared/NeoParking.Shared.Kernel/
COPY src/Modules/Access/NeoParking.Access.csproj                            src/Modules/Access/
COPY src/Modules/Management/NeoParking.Management.csproj                    src/Modules/Management/
COPY src/NeoParking.Api/NeoParking.Api.csproj                               src/NeoParking.Api/

# restore apenas os projetos de produção (testes ficam fora da imagem)
RUN dotnet restore src/NeoParking.Api/NeoParking.Api.csproj

# copia apenas src/ — tests/ não entra na imagem
COPY src/ src/

RUN dotnet publish src/NeoParking.Api/NeoParking.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─── stage 2: runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["/entrypoint.sh"]
