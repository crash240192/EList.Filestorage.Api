# Docker environment (Filestorage)

## Volume permissions (upload «Access to the path … is denied»)

Контейнер пишет файлы от пользователя **`app`**. Каталог на хосте
(`FILESTORAGE_DATA_PATH`, по умолчанию `/opt/elist/filestorage/data`) часто
создан от **root**. Тогда:

- **download** старых файлов работает (чтение);
- **upload** падает с HTTP 200 + `success: false` и текстом
  `Access to the path '/data/filestorage/<guid>' is denied` —
  это `UnauthorizedAccessException` ОС, **не** ACL приложения.

Образ с `docker-entrypoint.sh` при старте делает `chown app:app` **только
на каталог** тома (без `-R`) и затем drop privileges.

Срочный фикс без пересборки образа (на хосте):

```bash
# UID app внутри контейнера
docker compose exec filestorage-api id -u
# например 999
sudo chown 999:999 /opt/elist/filestorage/data
# рекурсия не обязательна — нужен write на сам каталог
docker compose restart filestorage-api
```

`ASPNETCORE_ENVIRONMENT` switches config at **runtime** (not at image build):

| Value | Effect |
|--------|--------|
| `Production` (default in Dockerfile) | Safe 500 body + `correlationId` |
| `Staging` | Detailed 500 (exception chain + stack) via `appsettings.Staging.json` |
| `Development` | Same as Staging for errors |

```bash
ASPNETCORE_ENVIRONMENT=Staging docker compose up -d
```

Full notes (API + UI + logs): see sibling repo `elist.api` → `docs/docker-environment.md`.

`appsettings.json` **and** `appsettings.Production.json` / `appsettings.Staging.json` are both expected in the image: base + overlay.
