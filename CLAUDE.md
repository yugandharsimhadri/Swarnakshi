# CLAUDE.md — working notes for AI assistants

Read `docs/05-progress.md` first, then `docs/01-architecture.md` and `docs/09-saas-tenancy.md`.

## Rules
- **Multi-tenant.** Every tenant row has `CompanyId`; a global query filter scopes reads and
  `SaveChangesAsync` stamps writes. Never add `WHERE CompanyId` by hand; never bypass the filter
  except with `BeginTenantScope` / `IgnoreQueryFilters()`. Unique indexes go on `(CompanyId, …)`.
- Logins are `username@companycode`. `EnterpriseAdmin` is a `PlatformUser`, has no company, and
  must never reach company data.
- Clean Architecture: `Domain` (no deps) ← `Application` ← `Infrastructure`/`Api`.
- Business logic in Domain/Application only. Controllers are thin. No logic in the React UI.
- Never hard-delete financial/inventory rows — cancel / reverse / void.
- Inventory is **site-level**. Projects consume from the shared pool. No per-project inventory.
- Inventory & financial side effects run only on approval→post, inside one transaction.
- Don't double count: purchase → inventory value; only consumption → project cost.
- Keep SQLite-agnostic: no provider-specific SQL/types. Provider chosen from `Database:Provider`.
- Money = `decimal(18,2)`. Timestamps = `DateTimeOffset`.
- Mobile-first UI. Minimal dependencies — justify every package.

## Commands
```
dotnet build
dotnet test                                   # unit + integration (fast) — UAT is gated out
dotnet test tests/Swarnakshi.UatTests -p:Uat=true   # browser UAT, headed (starts its own servers, minutes)
SWARNAKSHI_UAT_RUN_MODE=demo dotnet test tests/Swarnakshi.UatTests -p:Uat=true   # paced + captioned
dotnet run --project src/Swarnakshi.Api
dotnet ef migrations add <Name> --project src/Swarnakshi.Infrastructure --startup-project src/Swarnakshi.Api
cd web && npm run dev
```

UAT runs on its own ports (6070/6071) against a throwaway database — it never touches a running dev
server. See `docs/08-uat.md`.

## Conventions
- One `IEntityTypeConfiguration<T>` per entity in `Infrastructure/Persistence/Configurations`.
- DTOs + validators live in `Application/<Context>/`.
- Transaction numbers via `ITransactionSequenceService`.
- Every commit updates `docs/05-progress.md`.
