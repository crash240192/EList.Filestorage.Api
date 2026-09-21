# Docker environment (Filestorage)

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
