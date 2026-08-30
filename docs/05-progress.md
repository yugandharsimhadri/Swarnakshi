# 05 — Progress Log

Newest first. Every PR appends an entry: date, area, what changed, what's next, gotchas.

---

## 2026-08-31 — P0 vertical slice working (backend + frontend)

**Done**
- **Backend**: `Result` envelope, `AppException`, `IAppDbContext`, security abstractions.
  `AuthService` (login/refresh/logout/me, PBKDF2 + JWT access/refresh).
  `SiteService`, `ProjectService` (+`/summary` financial roll-up), `MasterService`
  (units, categories, subcategories, expense heads/subheads, labour cats, payment methods,
  project types read; materials + contractor/customer/supplier CRUD).
- **Infrastructure**: `AppDbContext` + configurations (unique codes, indexes, restrict-cascade),
  provider switch (Sqlite/SqlServer), design-time factory, `InitialCreate` migration,
  `MasterDataSeeder` (Indian construction defaults: 20 units, 19 categories, ~40 materials,
  31 expense heads w/ subheads, 17 labour cats, 8 payment methods, 7 project types, settings, Owner user),
  `DemoDataSeeder` (2 sites, 3 villas, 1 customer — all `IsDemo`).
- **Api**: JWT bearer (`MapInboundClaims=false`), `ExceptionMiddleware`, `RequiresPermission` filter,
  OpenAPI + Scalar UI (`/scalar/v1`), CORS, auto-migrate + seed on startup.
  Controllers: Auth, Sites, Projects, Lookups, Materials, Parties.
- **Frontend** (`web/`): Vite + React 19 + TS + Tailwind v4 + Zustand + React Router 7.
  Central `api` client (envelope unwrap + 1× auto token-refresh), `useAuth`, `useTheme` (light/dark),
  mobile-first `AppShell` (bottom nav), reusable `ui.tsx` (Card/StatCard/Chip/Button/Field/Sheet/…).
  Pages: Login, Dashboard, Sites (+create sheet), Projects (+create sheet), ProjectDetail (cost summary), Materials, More.
  Build: 255 KB / 81 KB gzip.
- **Verified in browser**: login → dashboard (demo KPIs) → project detail (budget variance, margin,
  cost-by-type, customer ledger). Mobile + desktop layouts. RBAC enforced server-side.

**Next (P1 — inventory)**
- `TransactionSequenceService` is wired but unused — hook into first numbered txn.
- Approval engine (`IApprovalService` + handler registry).
- Purchases (header/items) → `InventoryTransaction(PurchaseReceipt)` → weighted-avg `InventoryBalance`.
- Material requests → submit → owner approve → issue → `ProjectConsumption` + `ProjectExpense`.
- Inventory endpoints + screens; site inventory detail; material ledger.
- Simple-master CRUD endpoints (units, expense heads, etc.) — currently read-only.

**Gotchas**
- API dev URL fixed to `http://localhost:5080` via `Urls` in `appsettings.Development.json`
  (`--no-launch-profile` ignores `launchSettings.json`). Vite proxies `/api` there.
- Kill stray servers on Windows: `Get-CimInstance Win32_Process` + filter on CommandLine (`pkill` absent).
- `RowVersion` concurrency tokens deferred — SQLite has no auto `rowversion`; revisit in P5.
- npm gates install scripts: `npm approve-scripts esbuild` after `npm install`.
- Swashbuckle is broken on .NET 10 — using native `AddOpenApi()` + `Scalar.AspNetCore`.

---

## 2026-08-30 — P0 scaffold + design

**Done**
- Solution created: `Swarnakshi.Domain / Application / Infrastructure / Api` (.NET 10, Clean Architecture).
- Packages: EF Core 10 (Sqlite), EF Design, JwtBearer, FluentValidation.
- Architecture docs written: [01-architecture](01-architecture.md), [02-data-model](02-data-model.md),
  [03-workflows](03-workflows.md), [04-api](04-api.md).

**Next (P0)**
- Domain entities + enums (all contexts).
- `AppDbContext` + entity configurations + provider switch (Sqlite/SqlServer).
- Auth: password hasher, JWT service, `/api/auth/*`.
- Seed: roles/owner user, units, categories/subcategories, materials, expense heads/subheads,
  labour categories, payment methods, project types.
- Initial EF migration; auto-migrate + seed on startup in Development.
- Frontend scaffold: Vite + React + TS + Tailwind, apiClient, auth store, login, dashboard shell.

**Gotchas**
- `dotnet` Bash tool: working dir persists between calls — always `cd` to repo root or use absolute paths.
- NU1903 transitive vuln warnings from EF/SQLitePCLRaw — pin `SQLitePCLRaw.bundle_e_sqlite3` +
  `System.Security.Cryptography.Xml` in P5 polish.
- Solution file is `.slnx` (new XML format).
