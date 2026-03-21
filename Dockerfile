# ─── Build ───────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY SmartNest.sln .
COPY SmartNest.Shared/SmartNest.Shared.csproj   SmartNest.Shared/
COPY SmartNest.Server/SmartNest.Server.csproj   SmartNest.Server/
COPY SmartNest.Client/SmartNest.Client.csproj   SmartNest.Client/

RUN dotnet restore SmartNest.Server/SmartNest.Server.csproj

COPY . .

RUN dotnet publish SmartNest.Server/SmartNest.Server.csproj \
    -c Release -o /app/publish --no-restore

# ─── Runtime ─────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN mkdir -p /data/videos
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:${PORT:-5000}

EXPOSE 5000

ENTRYPOINT ["dotnet", "SmartNest.Server.dll"]
