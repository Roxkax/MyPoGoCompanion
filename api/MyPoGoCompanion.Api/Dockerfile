# The API image. Build from the repository root (it needs the shared build settings there):
#   docker build -f api/MyPoGoCompanion.Api/Dockerfile --build-arg VERSION=$(git rev-parse --short HEAD) -t mypogocompanion-api .
# Listens on port 8080 as a non-root user; /health shows VERSION.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the package layer is reused while only code changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY api/MyPoGoCompanion.Api/MyPoGoCompanion.Api.csproj api/MyPoGoCompanion.Api/
COPY api/MyPoGoCompanion.Data/MyPoGoCompanion.Data.csproj api/MyPoGoCompanion.Data/
RUN dotnet restore api/MyPoGoCompanion.Api/MyPoGoCompanion.Api.csproj

COPY api/MyPoGoCompanion.Api/ api/MyPoGoCompanion.Api/
COPY api/MyPoGoCompanion.Data/ api/MyPoGoCompanion.Data/
RUN dotnet publish api/MyPoGoCompanion.Api/MyPoGoCompanion.Api.csproj --no-restore -c Release -o /app /p:UseAppHost=false

# Chiseled: no shell or package manager, runs as a non-root user; enough for this app (invariant globalization).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
ARG VERSION=dev
ENV APP_VERSION=$VERSION
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyPoGoCompanion.Api.dll"]
