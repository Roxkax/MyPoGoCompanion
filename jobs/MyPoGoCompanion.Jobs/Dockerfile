# The scheduled jobs' image (Azure Container Apps Jobs). Build from the repository root (it needs the shared build
# settings there):
#   docker build -f jobs/MyPoGoCompanion.Jobs/Dockerfile --build-arg VERSION=$(git rev-parse --short HEAD) -t mypogocompanion-jobs .
# Runs one job and exits: the first argument names it (default: daily-import). APP_VERSION goes into the run log.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the package layer is reused while only code changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY jobs/MyPoGoCompanion.Jobs/MyPoGoCompanion.Jobs.csproj jobs/MyPoGoCompanion.Jobs/
COPY api/MyPoGoCompanion.Data/MyPoGoCompanion.Data.csproj api/MyPoGoCompanion.Data/
COPY api/MyPoGoCompanion.Engine/MyPoGoCompanion.Engine.csproj api/MyPoGoCompanion.Engine/
COPY api/MyPoGoCompanion.Observability/MyPoGoCompanion.Observability.csproj api/MyPoGoCompanion.Observability/
RUN dotnet restore jobs/MyPoGoCompanion.Jobs/MyPoGoCompanion.Jobs.csproj

COPY jobs/MyPoGoCompanion.Jobs/ jobs/MyPoGoCompanion.Jobs/
COPY api/MyPoGoCompanion.Data/ api/MyPoGoCompanion.Data/
COPY api/MyPoGoCompanion.Engine/ api/MyPoGoCompanion.Engine/
COPY api/MyPoGoCompanion.Observability/ api/MyPoGoCompanion.Observability/
RUN dotnet publish jobs/MyPoGoCompanion.Jobs/MyPoGoCompanion.Jobs.csproj --no-restore -c Release -o /app /p:UseAppHost=false

# Chiseled: no shell or package manager, runs as a non-root user; has the CA certificates for HTTPS downloads.
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled AS final
ARG VERSION=dev
ENV APP_VERSION=$VERSION
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "MyPoGoCompanion.Jobs.dll"]
CMD ["daily-import"]
