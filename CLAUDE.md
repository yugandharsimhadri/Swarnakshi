# CLAUDE.md — working notes for AI assistants

Read `docs/05-progress.md` first, then `docs/01-architecture.md`, `docs/09-saas-tenancy.md` and
`docs/06-deployment.md`.

## Rules
- **Multi-tenant.** Every tenant row has `CompanyId`; a global query filter scopes reads and
  `SaveChangesAsync` stamps writes. Never add `WHERE CompanyId` by hand; never bypass the filter
  except with `BeginTenantScope` / `IgnoreQueryFilters()`. Unique indexes go on `(CompanyId, …)`.
- Logins are `username@companycode`, or a bare 10-digit mobile number (canonicalised by
  `LoginIdentity.NormaliseMobile`; login-by-mobile crosses the tenant filter and fails helpfully if
  the number is registered with two companies). `EnterpriseAdmin` is a `PlatformUser`, has no
  company, and must never reach company data.
- Clean Architecture: `Domain` (no deps) ← `Application` ← `Infrastructure`/`Api`.
- Business logic in Domain/Application only. Controllers are thin. No logic in the React UI.
- Never hard-delete financial/inventory rows — cancel / reverse / void.
- Inventory is **site-level**. Projects consume from the shared pool. No per-project inventory.
- Inventory & financial side effects run only on approval→post, inside one transaction.
- Don't double count: purchase → inventory value; only consumption → project cost.
- **PostgreSQL only.** There is no second provider. Names are snake_case (`purchase_headers.total_amount`)
  via `UseSnakeCaseNamingConvention`, so hand-written SQL never quotes identifiers. Text comparison
  is case-sensitive on PostgreSQL: searches lower-case both sides, and user-typed codes are stored
  upper-case (`CodeGenerator.Canonical`). Every timestamp is UTC — Npgsql refuses any other offset.
  `dotnet test` needs a server it may create databases on: copy `testsettings.template.json` to
  `testsettings.json` (git-ignored) at the repo root.
- No secrets in the repo, and **every setting lives in a config file**, never only in an environment
  variable: the dev connection string in `dotnet user-secrets`, the server's in
  `appsettings.Production.json`, the tests' in `testsettings.json`, the migrator's in
  `migration.json` — the last three git-ignored, each with a committed `.template.json`.
- Money = `decimal(18,2)`. Timestamps = `DateTimeOffset`.
- Mobile-first UI. Minimal dependencies — justify every package.

## Commands
```
dotnet build
dotnet test                                   # unit + integration (fast) — UAT is gated out
dotnet test tests/Swarnakshi.UatTests -p:Uat=true   # browser UAT, headed (starts its own servers, minutes)
SWARNAKSHI_UAT_RUN_MODE=demo dotnet test tests/Swarnakshi.UatTests -p:Uat=true   # paced + captioned
dotnet run --project src/Swarnakshi.Api
dotnet run --project src/Swarnakshi.Api -- --migrate   # apply schema and exit (the deploy step)
dotnet ef migrations add <Name> --project src/Swarnakshi.Infrastructure --startup-project src/Swarnakshi.Api
cd web && npm run dev
deploy/scripts/Publish.ps1                     # build a deployable package into deploy/out
deploy/scripts/Deploy.ps1                     # install or upgrade on a server (elevated)
deploy/scripts/New-UpgradeScript.ps1 -From <migration>   # per-release SQL for a LIVE database
dotnet run --project tools/Swarnakshi.DataMigrator        # SQL Server -> PostgreSQL, once (migration.json)
```

The server is live. Rehearse every upgrade script against a copy of the live schema before it goes
near production (`docs/06c-db-upgrades.md`). Apply scripts with `psql -v ON_ERROR_STOP=1 -1 -f`:
without ON_ERROR_STOP psql carries on past a failure, and `-1` makes the file one transaction.
Moving the database (server to server, or to the cloud) is `docs/11-postgresql.md`.

UAT runs on its own ports (6070/6071) against a throwaway database — it never touches a running dev
server. See `docs/08-uat.md`.

## Conventions
- One `IEntityTypeConfiguration<T>` per entity in `Infrastructure/Persistence/Configurations`.
- DTOs + validators live in `Application/<Context>/`.
- Transaction numbers via `ITransactionSequenceService`.
- Every commit updates `docs/05-progress.md`.
