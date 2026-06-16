# Сборка из родительской директории, где лежат оба репозитория:
#   parent/
#     elist.filestorage/   (этот проект)
#     EList.Common/
#
#   cd parent
#   docker build -f elist.filestorage/Dockerfile -t crash240192/elist-filestorage-api .

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

RUN mkdir -p /data/filestorage \
    && chown -R app:app /data/filestorage

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
ENV STORAGE_PATH=/data/filestorage

VOLUME ["/data/filestorage"]

COPY --from=build /app/publish .

EXPOSE 80

USER app

ENTRYPOINT ["dotnet", "EList.Filestorage.Api.dll"]
