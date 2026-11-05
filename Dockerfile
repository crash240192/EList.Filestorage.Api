# Сборка из родительской директории, где лежат оба репозитория:
#   parent/
#     elist.filestorage/   (этот проект)
#     EList.Common/
#
#   cd parent
#   docker build -f elist.filestorage/Dockerfile -t crash240192/elist.filestorage.api .

ARG PROJECT_DIR=elist.filestorage

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG PROJECT_DIR
WORKDIR /src

COPY ${PROJECT_DIR}/ ./elist.filestorage/
COPY EList.Common/EList.Common/ ./EList.Common/EList.Common/

WORKDIR /src/elist.filestorage
RUN dotnet restore EList.Filestorage.sln
RUN dotnet publish EList.Filestorage.Api/EList.Filestorage.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app

# aspnet:6.0 не содержит user app (в отличие от .NET 8+)
# Entrypoint стартует от root → chown каталога тома → drop privileges на app.
# Иначе bind-mount с хоста (часто root:root) ломает upload при рабочих download.
RUN groupadd --system app \
    && useradd --system --gid app --home-dir /app --shell /usr/sbin/nologin app \
    && mkdir -p /data/filestorage /app/logs \
    && chown -R app:app /app /data/filestorage

ENV ASPNETCORE_ENVIRONMENT=Production
# Override at runtime for stage: -e ASPNETCORE_ENVIRONMENT=Staging
# See DOCKER.md
ENV ASPNETCORE_URLS=http://+:80
ENV STORAGE_PATH=/data/filestorage

VOLUME ["/data/filestorage"]

COPY --from=build --chown=app:app /app/publish .
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

EXPOSE 80

# Не USER app: entrypoint должен уметь chown смонтированного тома
ENTRYPOINT ["docker-entrypoint.sh"]
CMD ["dotnet", "EList.Filestorage.Api.dll"]

