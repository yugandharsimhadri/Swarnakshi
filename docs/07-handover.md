# 07 — Handover

Everything a new developer needs to pick up Swarnakshi. Read this, then
[01-architecture](01-architecture.md) and skim [05-progress](05-progress.md).

> **Status at handover (2026-08-31):** the full 6-phase plan (P0–P5) from the master spec is
> **complete and verified**. What's left is the optional P6 enhancement backlog (§11).
> Last commit on `main`: see `git log`. `dotnet build` + `dotnet test` (11) + `npm run build` all green.

---

## 1. Where the project stands

| Area | State |
|------|-------|
| Auth (JWT + refresh), users, roles, fine-grained permissions | ✅ done |
| **User administration** UI (create, role, active, Sub-Owner permissions, Supervisor site scoping, password reset) | ✅ done |
| Sites (+ edit), projects (+ edit), all master data + Indian-construction seed | ✅ done |
| Inventory — weighted-average ledger, opening stock, adjustments, returns, negative-stock guard | ✅ done |
| Purchases → site inventory (landed-rate valuation) + supplier payments | ✅ done |
| Material requests → Owner approval → issue → consumption → project cost | ✅ done |
| **Reusable approval engine** (one pipeline, per-entity handlers, txn-scoped side effects) | ✅ done |
| Project expenses, labour (approved), cost-by-head, cost-by-type | ✅ done |
| Contractors, contract works, contractor payments (approved, overpayment guard), ledger | ✅ done |
| Customers, receipts, receivables, ledger | ✅ done |
| Role-aware dashboard + 8 reports + CSV export | ✅ done |
| Audit log (status transitions), attachments (API + UI), optimistic concurrency | ✅ done |
| Skeleton loaders, confirm dialogs, light/dark theme, mobile-first shell | ✅ done |
| CI (GitHub Actions), 11 tests (pure + SQLite-in-memory integration) | ✅ done |
| **All 6 phases (P0–P5) complete** | ✅ |
| P6 nice-to-haves (simple-master admin UI, Scenario-B wiring, richer filters, transfers, …) | ⬜ backlog (§11) |

**Proof it works:** `dotnet test` (11 green) + four e2e scripts (`scratchpad/p1..p4test.mjs`, §9)
all pass against a single fresh DB. `dotnet build` and `cd web && npm run build` are clean.

---

## 2. Run it (first time, ~5 min)

Prereqs: **.NET 10 SDK**, **Node 20+**.

```bash
git clone https://github.com/yugandharsimhadri/Swarnakshi.git
cd Swarnakshi

# backend — creates + migrates + seeds swarnakshi.db on first run
dotnet run --project src/Swarnakshi.Api
#   API:  http://localhost:6051
#   docs: http://localhost:6051/scalar/v1   (interactive, Development only)

# frontend (separate terminal)
cd web
npm install
npm approve-scripts esbuild     # one-time: npm gates esbuild's postinstall
npm run dev
#   http://localhost:6050  (proxies /api → :6051)
```

Login: **`owner@swarnakshi.local` / `Owner@123`**
Dev seeds demo data (`Seed:Demo=true` in `appsettings.Development.json`): 2 sites, 3 villas,
1 customer — all rows tagged `IsDemo = true`.

```bash
dotnet test          # from repo root
```

To reset the DB: stop the API, delete `src/Swarnakshi.Api/swarnakshi.db*`, run again.

---

## 3. Repo map

```
src/
  Swarnakshi.Domain/            POCO entities + enums. NO dependencies, NO EF, NO framework.
    Common/BaseEntity.cs          BaseEntity (Id, CreatedAt/By, IsDemo)
                                  + AuditableEntity (Modified/Approved, Status, Remarks, ConcurrencyToken)
    Enums/Enums.cs                every enum (UserRole, TransactionStatus, InventoryTransactionType, …)
    Entities/*.cs                 grouped by context: Identity, Masters, Sites, Inventory, Procurement,
                                  Expenses, Contractors, Customers, Approvals
    Entities/Inventory.cs         ← InventoryBalance.Receive()/Issue() = the weighted-average maths

  Swarnakshi.Application/        use-case services, DTOs, validators, interfaces. References Domain + EF Core (for IQueryable).
    Abstractions/                 IAppDbContext, ICurrentUser, IJwtTokenService, ITransactionSequenceService, IFileStorage, …
    Common/                       Result envelope, AppException, PageQuery/PagedResult, SettingsService,
                                  ProjectCostWriter  ← the ONLY writer of posted ProjectExpense rows
    Security/Permissions.cs       permission-key constants + role → default set
    Approvals/Approvals.cs        ApprovalService + IApprovalHandler + ApprovalEntityTypes
    <Context>/                    Auth, Sites, Projects, Masters, Inventory, Procurement, Expenses,
                                  Contractors, Customers, Dashboard, Reports, Attachments, Users
    DependencyInjection.cs        AddApplication() — register every service + approval handler here

  Swarnakshi.Infrastructure/     EF Core, config, migrations, seed, JWT, hashing, storage. Implements Application abstractions.
    Persistence/AppDbContext.cs   DbSets + OnModelCreating (decimal/string/DateTimeOffset conventions,
                                  ConcurrencyToken) + SaveChangesAsync (audit stamping, AuditLog, token bump)
    Persistence/Configurations/   one class per entity area — unique indexes, FK delete behaviour
    Persistence/Migrations/       InitialCreate + P5_ConcurrencyToken
    Persistence/Seed/             MasterDataSeeder (idempotent, Indian construction defaults) + DemoDataSeeder
    Services/                     Pbkdf2PasswordHasher, JwtTokenService, TransactionSequenceService, SystemDateTimeProvider
    Storage/LocalFileStorage.cs   IFileStorage default impl (App_Data/uploads)
    DependencyInjection.cs        AddInfrastructure(config) — DbContext provider switch, JWT opts, storage

  Swarnakshi.Api/                thin controllers + composition root.
    Program.cs                    DI wiring, JWT bearer, CORS, exception middleware, OpenAPI+Scalar, auto-migrate+seed
    Common/                       ApiEnvelope, ExceptionMiddleware, CurrentUser (claims→ICurrentUser), RequiresPermission filter
    Controllers/*.cs              Auth, Sites, Projects, Masters (lookups), MastersCrud (simple-masters),
                                  Inventory, Procurement (purchases + material-requests), Approvals, Expenses,
                                  Contracts, CustomerPayments, Dashboard (+ Reports), Attachments, Users
    Persistence/DbInitializer.cs  MigrateAsync + seed on startup

tests/Swarnakshi.Tests/          xUnit + FluentAssertions (11). TestHost = real DI over SQLite in-memory + seeded masters.
    InventoryBalanceTests · CostFlowIntegrationTests · PaymentFlowTests · ConcurrencyTests

web/                             Vite + React 19 + TS + Tailwind v4 + Zustand + React Router 7
    src/lib/         api.ts (api + apiUpload + token refresh), types.ts, format.ts, useAsync.ts
    src/store/       auth.ts (Zustand), theme.ts
    src/components/  ui.tsx (kit), AppShell.tsx (bottom nav), SitePicker.tsx, AttachmentPanel.tsx
    src/pages/       Login, Dashboard, Sites, Projects, ProjectDetail (tabbed), Stock, Inventory,
                     MaterialRequests, Purchases, Approvals, Materials, Contractors, Customers,
                     Reports, Users, More

docs/                            01 architecture · 02 data model · 03 workflows · 04 API · 05 progress · 06 deploy · 07 handover
scratchpad/ (git-ignored temp)   p1test.mjs … p4test.mjs — Node e2e smoke scripts (recreate from patterns if lost)
.github/workflows/ci.yml         dotnet build+test + web build, on push/PR to main
```

---

## 4. The rules (do not break these)

1. **Clean Architecture direction:** `Domain` ← `Application` ← `Infrastructure`/`Api`.
   Domain references nothing. Application may use EF Core types but never Infrastructure.
2. **Business logic lives in Application/Domain.** Controllers are one-liners. The React app has
   zero business logic — it calls the API and renders.
3. **Never hard-delete financial or inventory rows.** Cancel / reverse / void
   (`Status = Cancelled`, amount 0 for audit; a reversing transaction for inventory).
4. **Inventory is site-level.** One `InventoryBalance` per `(SiteId, MaterialId)`. Projects
   *consume* from the shared pool — there is no per-project inventory.
5. **Inventory & financial side effects happen only on approval → post, inside one DB transaction.**
   See the approval engine (§5). Never mutate a balance or write a cost outside that path.
6. **No double counting.** A purchase becomes *inventory value*. Only the *consumed* portion
   becomes *project material cost*. `ProjectCostWriter` is the single place a posted
   `ProjectExpense` is created, so `Σ ProjectExpense == project total cost`, always.
   This is unit-tested — keep it that way.
7. **Money = `decimal(18,2)`. Timestamps = `DateTimeOffset` (UTC).**
8. **Provider-agnostic.** No SQLite-specific SQL/types. The `DateTimeOffset → UTC-ticks`
   value converter in `AppDbContext.OnModelCreating` is applied *only* under SQLite.
9. **Transaction numbers** come from `ITransactionSequenceService.NextAsync(prefix)` —
   `PUR / MATREQ / INV / EXP / LAB / CONPAY / CUSTPAY`. Never expose DB ids as business refs.
10. **Optimistic concurrency is automatic** for `AuditableEntity` — `ConcurrencyToken` is
   regenerated in `SaveChangesAsync` and enforced as a concurrency token. Load entities
   *tracked* (`FirstOrDefaultAsync`, not `AsNoTracking`) in write paths so EF has the original
   token for the `WHERE` clause. A stale write surfaces as HTTP 409.
11. **Every PR updates `docs/05-progress.md`** in the same commit as the code.

---

## 5. The approval engine — how to add a new approvable thing

This is the extension point you'll use most. One `ApprovalRequest` + `ApprovalHistory` pair
drives *every* approvable entity. To make entity `Foo` approvable:

1. **Add a constant** in `ApprovalEntityTypes` (`Approvals/Approvals.cs`):
   ```csharp
   public const string Foo = "Foo";
   ```
2. **Write a handler** implementing `IApprovalHandler`:
   ```csharp
   public class FooApprovalHandler(IAppDbContext db, IProjectCostWriter cost) : IApprovalHandler
   {
       public string EntityType => ApprovalEntityTypes.Foo;

       public async Task OnApprovedAsync(Guid id, ApprovalDecision d, Guid decidedBy, CancellationToken ct)
       {
           var foo = await db.Foos.FirstAsync(x => x.Id == id, ct);
           if (foo.Status == TransactionStatus.Posted) return;   // idempotent — handlers may re-enter
           // ... validate (throw AppException to block; check d.AllowOverride for owner overrides)
           foo.Status = TransactionStatus.Posted;
           foo.ApprovedBy = decidedBy; foo.ApprovedAt = DateTimeOffset.UtcNow;
           await db.SaveChangesAsync(ct);
           // ... side effects: inventory moves, cost.WriteAsync(...), ledger updates
       }

       public async Task OnRejectedAsync(Guid id, ApprovalDecision d, Guid decidedBy, CancellationToken ct)
       {
           var foo = await db.Foos.FirstOrDefaultAsync(x => x.Id == id, ct);
           if (foo is null) return;
           foo.Status = TransactionStatus.Rejected;
           await db.SaveChangesAsync(ct);
       }
   }
   ```
   The engine wraps `OnApprovedAsync` in a `BeginTransactionAsync` — if it throws, everything
   rolls back and the request stays `PendingApproval`.
3. **Register it** in `AddApplication()`: `services.AddScoped<IApprovalHandler, FooApprovalHandler>();`
4. **Submit from the service:** in `FooService.SubmitAsync`, set `foo.Status = PendingApproval`,
   `SaveChanges`, then `await approvals.SubmitAsync(ApprovalEntityTypes.Foo, foo.Id, foo.TxnNumber, siteId, projectId, amount, ct);`
5. The Owner approves/rejects via `POST /api/approvals/{id}/approve|reject` — already generic,
   nothing to add. It shows up in the frontend Approval Center automatically (add a friendly
   label in `web/src/pages/Approvals.tsx` → `label` map).

Existing handlers to copy from: `PurchaseApprovalHandler`, `MaterialRequestApprovalHandler`,
`LabourApprovalHandler`, `ContractorPaymentApprovalHandler`.

---

## 6. How costs reach a project (read once, internalise)

```
Purchase.post ─► InventoryLedger.ReceiveAsync ─► InventoryBalance.Receive (weighted avg)     [NO project cost yet]

MaterialRequest.issue ─► InventoryLedger.IssueAsync (rate = current weighted avg)
                       ─► InventoryBalance.Issue
                       ─► ProjectCostWriter.WriteMaterialCostAsync(qty × rate)  ─► ProjectExpense(Material, Posted)

LabourEntry approved      ─► ProjectCostWriter.WriteAsync(Labour, amount)       ─► ProjectExpense(Labour, Posted)
ContractorPayment approved─► ProjectCostWriter.WriteAsync(Contractor, amount)   ─► ProjectExpense(Contractor, Posted)
                          └► ContractWork.TotalPaid += amount; Balance = ContractAmount − TotalPaid
Manual expense            ─► ProjectExpenseService.CreateAsync                  ─► ProjectExpense(Direct/…, Posted)

ProjectService.SummaryAsync = Σ ProjectExpense grouped by ExpenseType  (single source ⇒ no double count)
```

`CustomerPayment` does **not** touch cost — it's revenue: `Outstanding = SaleValue − Σ receipts`.

---

## 7. Recipe: add a new feature (e.g. "Machinery Log")

**Backend**
1. `Domain/Entities/…` — add the entity (inherit `BaseEntity` or `AuditableEntity`). Add nav
   properties. If it has a business code / txn number, plan for a unique index.
2. `Infrastructure/Persistence/Configurations/Configurations.cs` — add an
   `IEntityTypeConfiguration<T>` (unique indexes, `OnDelete(DeleteBehavior.Restrict)` for
   reference FKs, `Cascade` only for owned children).
3. `Application/Abstractions/IAppDbContext.cs` — add `DbSet<T> Machinery { get; }`.
   `Infrastructure/Persistence/AppDbContext.cs` — add `public DbSet<T> Machinery => Set<T>();`.
4. `Application/<Context>/MachineryService.cs` — DTOs + `record Save…Request` +
   `AbstractValidator<>` + `IMachineryService` + impl. Use `PageQuery`/`ToPagedAsync`,
   `AsNoTracking()` for reads, project to DTOs (never return entities).
5. Register in `AddApplication()`.
6. `Api/Controllers/MachineryController.cs` — thin, `[Authorize]`,
   `[RequiresPermission(Permissions.X)]` on writes, return `this.Envelope(...)` /
   `this.EnvelopeCreated(...)`.
7. If a new permission is needed: add the constant to `Permissions.cs` and slot it into the
   relevant `ForRole(...)` sets.
8. `dotnet dotnet-ef migrations add AddMachinery --project src/Swarnakshi.Infrastructure --startup-project src/Swarnakshi.Api --output-dir Persistence/Migrations`
9. Add a test in `tests/Swarnakshi.Tests` using `TestHost` (see `CostFlowIntegrationTests`).

**Frontend**
10. `web/src/lib/types.ts` — add the TS interface + any enum-name maps.
11. `web/src/pages/Machinery.tsx` — use `useAsync(() => api<…>("/machinery", { query }))`,
    the `ui.tsx` kit (`Card`, `StatCard`, `Chip`, `Button`, `Field`, `Input`, `Select`,
    `Sheet`, `Confirm`, `EmptyState`, `Spinner`, `SkeletonList`, `ErrorText`, `PageHeader`).
    For file uploads use `apiUpload` + `<AttachmentPanel entityType=… entityId=… canEdit=… />`.
12. `web/src/App.tsx` — add `<Route path="machinery" …>`.
13. Nav: add to `AppShell.tsx` bottom nav (max 5) **or** link from `Stock.tsx` / `More.tsx` /
    a project-detail tab in `ProjectDetail.tsx`.

**Both**
14. Update `docs/04-api.md` and `docs/05-progress.md`. Commit.

---

## 8. Frontend conventions

- **API:** always `import { api } from "@/lib/api"`. It unwraps `{success,data}`, attaches the
  bearer token, and auto-refreshes the access token once on 401. Errors throw `ApiError`
  `{ message, errors[], status }`.
- **Data fetching:** `useAsync(fn, deps)` → `{ data, error, loading, reload }`. Keep `fn` pure.
- **Auth/permissions:** `useAuth((s) => s.can("permission.key"))` to gate UI. The backend
  enforces it regardless — UI gating is cosmetic.
- **Money:** `money()`, `moneyShort()` (₹1.2L / ₹3.4Cr), `num()`, `dateStr()` from `@/lib/format`.
- **Theme:** CSS variables in `index.css`, toggled by `useTheme`. Use the semantic Tailwind
  colors (`bg-surface`, `text-text-dim`, `text-ok`, `text-warn`, `text-danger`, `bg-brand`).
- **Mobile-first:** design for 375px. Bottom sheets (`Sheet`) for forms, cards not tables,
  `Confirm` for irreversible actions. Wide content scrolls inside `overflow-x-auto`.
- **Routes** are lazy-free for now (small bundle); code-split later if it grows past ~300 KB.

---

## 9. Testing

`tests/Swarnakshi.Tests` (11 tests):
- `InventoryBalanceTests` (5) — pure maths: weighted average, no-double-count identity, negative-stock.
- `CostFlowIntegrationTests` (2) — real services over SQLite in-memory: full
  purchase→approve→issue→cost flow with the no-double-count assertion; issue blocked pre-approval.
- `PaymentFlowTests` (3) — labour posts only after approval; contractor overpayment block + override;
  customer-required rule.
- `ConcurrencyTests` (1) — a stale update to an `AuditableEntity` is rejected.
- `TestHost.CreateAsync()` builds the real DI graph (`AddApplication()` + SQLite in-memory +
  seeded masters) and a `FakeCurrentUser` with all permissions. Copy it for any new integration test.

Manual e2e (need the API running on :6051): `node scratchpad/p1test.mjs` … `p4test.mjs`.
These are throwaway scripts in the git-ignored scratchpad — they exercise the full flow per phase
(P1 inventory, P2 expenses/contractors, P3 customers, P4 dashboard/reports). Recreate from the
patterns if lost, or promote them to xUnit.

CI runs `dotnet build+test` and `npm run build` on every push/PR to `main`.

---

## 10. Gotchas (accumulated — save yourself the debugging)

| # | Gotcha |
|---|--------|
| 1 | **SQLite can't `ORDER BY` / compare `DateTimeOffset`.** Handled by a value converter (UTC ticks) applied only under SQLite in `AppDbContext.OnModelCreating`. Don't remove it. |
| 2 | **EF `GroupBy` with a navigation in the key** (`GroupBy(e => new { e.Head.Name })`) doesn't translate on SQLite. Group by the id, then map names from a second query. |
| 3 | **EF can't project straight to `object?[]`.** Fetch an anonymous type, map to array in memory (see `ReportsService`). |
| 4 | **API dev URL is pinned to `http://localhost:6051`** via `"Urls"` in `appsettings.Development.json` — `dotnet run --no-launch-profile` ignores `launchSettings.json`. Vite proxies `/api` there. |
| 5 | **`Jwt:Key` must be ≥32 chars in non-Development** or the app refuses to start. Dev uses a hard-coded insecure key. |
| 6 | **Swashbuckle is broken on .NET 10** — we use the native `AddOpenApi()` + `Scalar.AspNetCore` (`/scalar/v1`). |
| 7 | **`ProjectStatus` and `TransactionStatus` share int values but mean different things.** Frontend: use `ProjectStatusName`, not `TxnStatusName`, for projects. |
| 8 | **Approval handlers must be idempotent** — the poster checks `if (Status == Posted) return;`. |
| 9 | **Landed rate for purchase valuation** = `LineTotal / Quantity` (incl. tax/discount), not the raw entered rate. |
| 10 | **`npm` gates install scripts** — run `npm approve-scripts esbuild` after `npm install`. |
| 11 | Windows: kill stray dev servers with `Get-CimInstance Win32_Process -Filter "name='dotnet.exe'"` + filter on `CommandLine` (`pkill` isn't available in Git Bash here). |
| 12 | **`<a download>` blob export works in a real browser** but the in-app preview sandbox blocks it — test the report CSV / attachment download endpoint directly. |
| 13 | `InitialCreate` was **regenerated clean** while pre-production; `P5_ConcurrencyToken` was additive. Once real data exists anywhere, additive migrations only. |
| 14 | **`IMutableProperty.IsConcurrencyToken` is a settable property, not a `Set…` method** — `prop.IsConcurrencyToken = true;` in `OnModelCreating`. |
| 15 | **Never send `FormData` through `api()`** — it forces `Content-Type: application/json` and multipart breaks. Use `apiUpload(path, formData)` (in `web/src/lib/api.ts`). |
| 16 | **`DbUpdateConcurrencyException` → 409** is handled centrally in `ExceptionMiddleware`. Don't catch it in services. |
| 17 | The first `dotnet build` after `restore` prints up to 2 NuGet-audit lines; incremental builds are 0-warning. `NuGetAuditMode=direct` in `Directory.Build.props` already suppresses the transitive NU1903 noise from EF Core 10 GA deps. |

---

## 11. Backlog (P6 — optional, none blocking)

The 6-phase plan (P0–P5) is complete. These are enhancements:

1. **Simple-master admin UI** — screens for units / material categories+subcategories /
   expense heads+subheads / labour categories / payment methods / project types.
   Backend is done: `/api/simple-masters/{kind}` (POST/PUT/DELETE), `SimpleMasterKind` enum.
2. **Material request Scenario B** UI — a `RequestType = Purchase` request that, on approval,
   auto-creates a linked `PurchaseHeader` (`PurchaseHeader.MaterialRequestId` FK exists;
   `PurchaseService.CreateAsync` accepts the link; just needs UI + a small service tweak to
   pre-fill from the request).
3. Richer report filters (date pickers, site/project selectors) + more reports
   (material ledger, stock valuation by method, project profitability detail).
4. Inter-site material **Transfer** (`InventoryTransactionType.Transfer` enum exists, no service):
   one issue from source site + one receipt at destination, same transaction.
5. PWA manifest + offline master-data cache (Zustand persist / IndexedDB).
6. Multi-company layer — currently single company per deployment; `CompanyId` deliberately not
   modelled (assumption #1 in [01-architecture](01-architecture.md)). Adding it touches every
   query — plan carefully.
7. Notifications (approval pending → Owner; low stock → Supervisor) — email or in-app.
8. Configurable approval for **direct expenses** and **inventory adjustments** (settings keys
   `expense.needs_approval` / `inventory.adjustment_needs_approval` — the second is honoured today
   only as an Owner-only gate, not a full approval flow).

---

## 12. Deploying

See [06-deployment.md](06-deployment.md) — `dotnet publish`, env vars, static frontend hosting,
SQLite → SQL Server switch, backups.

---

## 13. Branch / PR workflow

- Branch per feature: `feat/<area>-<short>` (e.g. `feat/simple-master-ui`). PR into `main`.
- Keep commits scoped; conventional-ish prefixes (`feat(p6): …`, `fix: …`, `test: …`, `docs: …`).
- CI must be green. Update `docs/05-progress.md` in the PR.
- Don't rewrite working code for style. Match the surrounding conventions.

---

## 14. Screen ↔ endpoint map

| Screen (route) | Primary endpoints | Who |
|---|---|---|
| Login (`/login` implicit) | `POST /api/auth/login`, `/refresh`, `/me` | all |
| Dashboard (`/`) | `GET /api/dashboard` | all (role-shaped) |
| Sites (`/sites`) | `GET/POST/PUT /api/sites` | view all · edit `sites.manage` |
| Projects (`/projects`, `/projects/:id`) | `/api/projects` + `/{id}/summary`; per-tab: `/expenses`, `/labour`, `/contracts`, `/contractor-payments`, `/customer-payments`, `/expenses/cost-by-head` | view all · create/edit `projects.manage` |
| Stock hub (`/stock`) | — | all |
| Site Inventory (`/stock/inventory`, `/stock/inventory/:site/:material`) | `/api/inventory`, `/api/inventory/{site}/{material}`, `/api/inventory/transactions` | `inventory.view` |
| Material Requests (`/stock/requests…`) | `/api/material-requests` + `/submit` `/issue` `/cancel` | create `material_request.create` |
| Purchases (`/stock/purchases…`) | `/api/purchases` + `/submit` `/payments` | `purchase.create` |
| Materials master (`/materials`) | `/api/materials` | view all · edit `masters.manage` |
| Approval Center (`/approvals`) | `GET /api/approvals`, `/{id}/approve` `/reject` `/history`, `/count` | `approvals.decide` |
| Contractors (`/contractors`) | `/api/contractors`, `/api/contractor-payments/ledger/{id}` | `masters.manage` |
| Customers (`/customers`) | `/api/customers`, `/api/customer-payments/ledger/{id}` | `masters.manage` |
| Reports (`/reports`, `/reports/:slug`) | `GET /api/reports/*` (+ `?format=csv`) | `reports.view` |
| Users (`/users`) | `/api/users` + `/permission-keys` `/{id}` `/password` `/permissions` `/sites` | `users.manage` |
| More (`/more`) | — theme, logout, links | all |
| (any detail) Documents panel | `/api/attachments` (+ `/{id}/download` `/{id}` DELETE) | entity's edit permission |

**Role → default permissions** (in `Permissions.ForRole`): Owner = all · Sub-Owner =
`inventory.view` + `reports.view` (extend per-user in the Users screen) · Supervisor =
`inventory.view`, `material_request.create`, `purchase.create`, `projects.manage`, `reports.view` ·
Accountant = `expense.create`, `labour.create`, `contract.manage`, `contractor_payment.create`,
`customer_payment.create`, `inventory.view`, `reports.view`.
