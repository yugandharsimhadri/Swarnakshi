# 06 — Build & Deployment

## Local development

```bash
dotnet restore
dotnet run --project src/Swarnakshi.Api          # http://localhost:6051, docs at /scalar/v1
cd web && npm install && npm approve-scripts esbuild && npm run dev   # http://localhost:6050
```

The SQLite DB (`src/Swarnakshi.Api/swarnakshi.db`) is created, migrated and seeded on first run.
`appsettings.Development.json` sets `Seed:Demo=true` (2 sites, 3 villas, tagged `IsDemo`).

## Database migrations

```bash
dotnet dotnet-ef migrations add <Name> --project src/Swarnakshi.Infrastructure --startup-project src/Swarnakshi.Api --output-dir Persistence/Migrations
dotnet dotnet-ef database update       --project src/Swarnakshi.Infrastructure --startup-project src/Swarnakshi.Api
```

Startup calls `db.Database.MigrateAsync()` automatically, so a deployed API applies pending
migrations on boot.

## Production build

### Backend
```bash
dotnet publish src/Swarnakshi.Api -c Release -o publish
```
Set via environment (never commit secrets):
| Variable | Purpose |
|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Jwt__Key` | ≥32-char signing secret (**required** outside Development — the app refuses to start without it) |
| `Jwt__Issuer`, `Jwt__Audience` | token iss/aud |
| `ConnectionStrings__Default` | e.g. `Data Source=/var/lib/swarnakshi/swarnakshi.db` or a SQL Server connection string |
| `Database__Provider` | `Sqlite` (default) or `SqlServer` |
| `Seed__OwnerEmail`, `Seed__OwnerPassword` | first Owner login (change immediately after first login) |
| `Cors__Origins__0` | the deployed frontend origin |
| `Storage__LocalRoot` | attachments directory |

Run: `dotnet publish/Swarnakshi.Api.dll`. Put it behind a reverse proxy (nginx / IIS / Caddy) for TLS.

### Frontend
```bash
cd web && npm ci && npm run build      # outputs web/dist/
```
Serve `web/dist/` as static files. Point the build at the API by either:
- hosting frontend and API on the same origin (recommended — no CORS), or
- setting a proxy / rewrite so `/api/*` reaches the API.

`web/vite.config.ts` proxies `/api` to `:6051` for dev only.

## Switching SQLite → SQL Server

1. `Database__Provider=SqlServer`, `ConnectionStrings__Default=<sql server conn>`.
2. Generate SQL Server migrations into a separate folder:
   `dotnet ef migrations add InitialCreate --project src/Swarnakshi.Infrastructure --startup-project src/Swarnakshi.Api --output-dir Persistence/Migrations/SqlServer -- --provider SqlServer`
   (add a provider switch in `DesignTimeDbContextFactory` / a second `AppDbContext` migrations assembly).
3. No domain/application code changes — all queries are provider-agnostic; the `DateTimeOffset`
   value converter is applied only under SQLite.

## Backups

SQLite: stop the API (or use `.backup`), copy `swarnakshi.db`. Attachments: back up `Storage:LocalRoot`.

## Health & docs

- `GET /health` → `{ "status": "ok" }`
- `GET /scalar/v1` (Development only) — interactive API reference
- `GET /openapi/v1.json` — OpenAPI document
