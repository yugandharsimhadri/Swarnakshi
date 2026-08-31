# 05 — Progress Log

Newest first. Every PR appends an entry: date, area, what changed, what's next, gotchas.

---

## 2026-08-31 — P4 dashboards & reports (backend + frontend)

**Done — backend**
- `DashboardService`: single `/api/dashboard` endpoint, role-aware KPI set
  (Owner/SubOwner: projects, sites, inventory value, project cost, month purchases/expenses,
  receivable, payable, low stock · Accountant: payables/receivables, month receipts, draft
  counts · Supervisor: my sites/projects, pending & approved-not-issued requests) +
  recent transactions + pending-approval count.
- `ReportsService`: inventory stock / low-stock / purchase register / consumption register /
  project cost summary / contractor outstanding / customer outstanding / company summary.
  Returns a generic `ReportTable {title, columns, rows}`.
- `ReportsController` with `?format=csv` (generic table → CSV, RFC-4180 quoting). `ReportsView` gate.
- Verified (`scratchpad/p4test.mjs`): dashboard payload, every report table, CSV download.

**Done — frontend**
- Dashboard rewritten to consume `/api/dashboard` (role KPIs + recent activity + approvals card).
- **Reports** hub (grouped list) + generic `ReportView` (scrollable table + Export CSV via blob).
- More menu: Reports link. Bundle ~305 KB / 91 KB gzip.
- Verified dashboard + report view against live data.

**Next (P5 — polish)**
- Pin `SQLitePCLRaw.bundle_e_sqlite3` + `System.Security.Cryptography.Xml` to clear NU1903 warnings.
- `RowVersion` concurrency tokens (SQLite trigger or app-generated token).
- Attachments endpoint + UI (invoices / receipts) on `IFileStorage`.
- AuditLog writes on posts/approvals; global request logging.
- UX: skeleton loaders, empty/error states pass, confirm dialogs for irreversible actions,
  form validation polish, offline-friendly caching of master lists.
- Production build instructions + `dotnet publish` + static frontend hosting notes.
- Test project (inventory valuation, approval flow, no-double-count invariants).

**Gotchas**
- EF can't project straight to `object?[]` — fetch an anonymous type, map to array in memory.
- `<a download>` blob export works in a real browser; the in-app Browser pane sandbox blocks it
  (test the endpoint directly instead).

---

## 2026-08-31 — P3 customers & receivables (backend + frontend)

**Done — backend**
- `CustomerPaymentService`: record receipts against a project's assigned customer (posts directly,
  Accountant permission), cancel = amount 0 + `Cancelled` for audit, per-customer `LedgerAsync`
  (sale value / received / outstanding + Sale + Receipt rows across all the customer's projects).
- Blocks receipts when the project has no customer (409). `CustomerPaymentsController`. No migration.
- Verified e2e (`scratchpad/p3test.mjs`): ₹10L + ₹15L receipts → project summary received ₹25L /
  outstanding ₹55L, customer ledger matches, no-customer guard 409.

**Done — frontend**
- Project detail: **Customer tab** — sale/received/outstanding card, receipt list, "Record receipt" sheet.
- **Customers** master page (More → Customers): list, create, expandable ledger row.
- Verified against live P3 data. Bundle ~298 KB / 89 KB gzip.

**Next (P4 — reporting & dashboards)**
- `/api/dashboard` role-aware payload (owner / supervisor / accountant) — replace the client-side
  aggregation the Dashboard does now.
- Reports: inventory (stock, valuation, ledger, purchase register, consumption, low-stock),
  project (cost summary, budget vs actual, profitability), contractor & customer outstanding,
  company (purchase summary, expense summary, inventory value). `?format=csv` export.
- Report screens + a company-level financial overview for the Owner.

**Gotchas**
- Customer receipts require `Project.CustomerId`; the Customer tab shows an empty state prompting
  a project edit when it's missing.

---

## 2026-08-31 — P2 expenses, labour, contractors (backend + frontend)

**Done — backend**
- `ProjectExpenseService`: manual direct/transport/machinery/other expenses (Contractor & Labour
  types rejected here — they flow from their own screens); cancel = set amount 0 + `Cancelled`
  (keeps the row for audit, drops it from roll-ups); `cost-by-head` grouping.
- `LabourService` + `LabourApprovalHandler`: draft → submit → Owner approve → posts
  `ProjectExpense(Labour)` under the "Labour" head. Category + period + amount, no worker master.
- `ContractWorkService`: contract works with live `TotalPaid` / `Balance`; contract amount can't
  drop below amount already paid.
- `ContractorPaymentService` + `ContractorPaymentApprovalHandler`: Accountant creates → Owner
  approves → posts `ProjectExpense(Contractor)` + updates the contract balance. Payment over the
  contract balance is blocked unless the Owner approves **with override**. `LedgerAsync` returns
  contracted / paid / outstanding + rows.
- `ProjectService.SummaryAsync` now derives every cost bucket from `ProjectExpense` by type only
  (single source of truth → provably no double counting).
- Controllers: Expenses, Labour, Contracts, ContractorPayments. No migration (entities unchanged).

**Verified e2e** (`scratchpad/p2test.mjs`): labour ₹8,000 + transport ₹3,500 + contract ₹2,50,000
with a ₹50,000 payment → project summary material 0 / labour 8,000 / contractor 50,000 / other 3,500
/ total 61,500; contract balance ₹2,00,000; cost-by-head {Miscellaneous 50k, Labour 8k, Transport 3.5k};
contractor ledger contracted 250k / paid 50k / outstanding 200k; overpayment approval → 409.

**Done — frontend**
- **Project detail** rebuilt with tabs: Overview (cost-by-type + cost-by-head + customer),
  Expenses (add sheet), Labour (add + submit), Contracts (new sheet, live balance),
  Payments (contractor payment sheet with contract picker + submit).
- **Contractors** master page (More → Contractors): list, create, expandable ledger row.
- More menu adds Contractors + Approval Center links. Bundle 293 KB / 87 KB gzip.
- Verified project detail + contractors render against live P2 data.

**Next (P3 — customers & receivables)**
- Customer master UI (Party CRUD already exists in backend).
- `CustomerPaymentService` (+ optional approval) → posts to customer ledger; project receivables.
- Customer tab on project detail; customer ledger screen.

**Gotchas**
- `GroupBy` with a navigation property in the key (`e.Head.Name`) doesn't translate on SQLite —
  group by id, then map names from a second query.
- Project status chip must use `ProjectStatusName`, not `TxnStatusName` (different enums, same ints).

---

## 2026-08-31 — P1 inventory + procurement + approvals (backend + frontend)

**Done — backend**
- **Approval engine**: `ApprovalService` + `IApprovalHandler` registry (keyed by entity type).
  `SubmitAsync` / `DecideAsync`; approve runs the handler's side effects inside one DB transaction
  then marks `Posted`. `PurchaseApprovalHandler`, `MaterialRequestApprovalHandler`.
- **Inventory** (`InventoryService`): weighted-average `Receive`/`Issue` on `InventoryBalance`,
  signed `InventoryTransaction` ledger with full source traceability, opening stock, adjustments
  (Owner-gated when `inventory.adjustment_needs_approval`), returns (reverse project cost).
  Negative-stock blocked unless `inventory.allow_negative_stock` per site.
- **Purchases** (`PurchaseService` + `PurchasePoster`): draft → submit → (approval if
  `purchase.needs_approval`) → post. Each item posts a `PurchaseReceipt` at *landed* rate
  (line total incl. tax/discount ÷ qty). Supplier payments track `PaidAmount`/`BalanceAmount`.
- **Material requests** (`MaterialRequestService` + `MaterialRequestIssuer`): draft → submit →
  Owner approve (sets `ApprovedQty`) → issue. Issue moves stock as `ProjectConsumption` and books
  project material cost via `ProjectCostWriter` at the weighted-avg rate. Supports partial issue.
- **`ProjectCostWriter`**: the single writer of posted `ProjectExpense` rows → no double counting.
- **`SimpleMasterService`**: CRUD for units / material & expense categories+subheads / labour cats /
  payment methods / project types. Delete blocked when referenced.
- **`SettingsService`**: per-site → global fallback; typed getters.
- SQLite `DateTimeOffset` → UTC-ticks value converter (SQLite can't `ORDER BY` DateTimeOffset).
  Migrations collapsed to a single clean `InitialCreate`.
- Controllers: Inventory, Purchases, MaterialRequests, Approvals, SimpleMasters.

**Verified end-to-end** (`scratchpad/p1test.mjs`): 2 purchases (200@400 + 100@450) →
balance 300 @ ₹416.67 = ₹1,25,000 → request 50 bags → submit → approve → issue →
consumption −50 @ ₹416.67, project material cost ₹20,833.33, remaining 250 @ ₹416.67 = ₹1,04,167
(₹1,25,000 = ₹20,833 consumed + ₹1,04,167 in stock — no double counting).
Negative-stock request blocked with 409. Full ledger + traceability confirmed in the UI.

**Done — frontend**
- Bottom nav: Home · Sites · Projects · **Stock** · More. Dashboard shows an
  "Approvals waiting" card for Owners (live count).
- **Stock** hub → Site Inventory (site picker + low-stock filter + value KPIs),
  Material Inventory detail (KPIs + full ledger), Material Requests (list / new multi-item /
  detail with submit+issue+cancel), Purchases (list / new multi-line / detail with submit+pay),
  **Approval Center** (approve / reject each pending item).
- Bundle 276 KB / 85 KB gzip.

**Next (P2 — expenses & contractors)**
- Project direct/other expenses + Labour entries (with approval for labour payments).
- Contractors master UI; ContractWork; ContractorPayment (Accountant creates → Owner approves →
  posts project cost + contractor ledger). Handlers: `ContractorPaymentApprovalHandler`,
  `LabourApprovalHandler`.
- Project detail: wire Expenses / Materials / Labour / Contracts tabs.

**Gotchas**
- Browser-pane `computer` clicks time out while the pane is hidden — screenshots, `navigate`,
  `get_page_text`, `javascript_tool` still work; drive the SPA via those when verifying.
- `IApprovalHandler` implementations must be idempotent on re-entry (poster checks `Posted`).
- Landed purchase rate is used for valuation, not the raw line rate.

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
