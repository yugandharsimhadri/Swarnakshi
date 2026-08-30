# 01 — Architecture & Implementation Report

## 1. Positioning

Swarnakshi is a **Construction Business Operating System**: it tracks money and material from
purchase → site inventory → project consumption → project cost, plus contractor and customer
ledgers. It is not a plain expense-entry app.

## 2. Three organisational levels

| Level | Owns |
|-------|------|
| **Company / Owner** | users, roles, masters (materials, units, expense heads, labour categories, payment methods, project types), contractors, customers, suppliers, settings |
| **Site** (physical location) | exactly **one common inventory pool**; many projects |
| **Project / Villa** | customer, customer payments, expenses, material consumption, labour, contracts, contractor payments, budget vs actual, margin |

Inventory is **site-level, never project-level**. Projects *consume* from the shared pool.

## 3. Clean Architecture

```
Api  ──►  Application  ──►  Domain
Infrastructure ──► Application, Domain      (implements Application interfaces)
Api ──► Infrastructure   (composition root only; controllers depend on Application abstractions)
```

- **Domain** — POCO entities, enums, invariants, value calculations. No EF, no framework.
- **Application** — DTOs, `I*Service` / `I*Repository` interfaces, FluentValidation validators,
  use-case services (orchestration, transactions expressed via `IUnitOfWork`).
- **Infrastructure** — `AppDbContext`, `IEntityTypeConfiguration<T>` per entity, migrations,
  seeders, JWT token service, password hasher, `ICurrentUser`, generic repository + UoW.
- **Api** — thin controllers, `ProblemDetails` exception middleware, auth wiring, Swagger.

### SQLite → SQL Server portability
- No SQLite-specific SQL, functions, or column types in domain/application code.
- Money stored as `decimal(18,2)`; `DateTimeOffset` for timestamps.
- Provider chosen in `Infrastructure` from `Database:Provider` config (`Sqlite` | `SqlServer`).
- Migrations folder per provider when SQL Server is added.

## 4. Cross-cutting

| Concern | Approach |
|---------|----------|
| Validation | FluentValidation in Application; model-level guard clauses in Domain |
| Errors | Global middleware → `{ success, message, errors[] }`; no stack traces to client |
| Logging | `ILogger` structured; no PII / secrets in logs |
| Auth | JWT bearer; `[Authorize(Roles=...)]` + policy checks in services (never UI-only) |
| Audit | `AuditableEntity` (Created/Modified/Approved by+at); `AuditLog` table; soft-cancel not delete |
| Numbering | `TransactionSequence` table → `PUR-2026-00001` etc., generated in a transaction |
| Concurrency | `RowVersion` on financial + inventory entities |
| Performance | `AsNoTracking` reads, DTO projection, pagination, server-side filtering, indexes, cached masters |

## 5. Roles

`Owner` (full + all approvals), `SubOwner` (configurable, view + optional approvals),
`Supervisor` (raise requests/purchases, operational entry, no owner approvals),
`Accountant` (expenses, labour/contractor/customer payments, no owner approvals).

Permissions resolved centrally (`IPermissionService`), enforced in Application services.

## 6. Approval engine (reusable)

One `ApprovalRequest` + `ApprovalHistory` pair drives every approvable entity
(material request, purchase, contractor payment, labour payment, inventory adjustment, configured expenses).
State machine: `Draft → Submitted → PendingApproval → Approved → Posted` / `Rejected` / `Cancelled`.
Inventory/financial effects happen **only** on the `Approved → Posted` transition, inside a DB transaction.

## 7. Inventory valuation

Configurable per site (`Settings`): default **Weighted Average Cost**. Extensible to FIFO / Manual.
Consumption rate = current weighted-average of the site+material balance at issue time.

## 8. Costing flow (no double counting)

Purchase = cash outflow / payable → becomes **inventory value**.
Only the **consumed** portion becomes **project material cost**. Unconsumed stays inventory value.
Project total cost = material consumption + labour + contractor payments (posted) + direct/other expenses.
Project margin = contract/sale value − project total cost.

## 9. Frontend

Mobile-first SPA: bottom nav, cards, bottom sheets, FAB, status chips, segmented controls.
Central `apiClient` (fetch wrapper + auth header + error normalisation), central auth store,
central permission map. Light/dark theme via CSS variables. Route-level code splitting.

## 10. Build order

P0 foundation (auth, users/roles, sites, projects, masters, DB, seed) →
P1 inventory (materials, balances, purchases, requests, approval, movement, consumption) →
P2 expenses (heads/subheads, project expenses, labour, contractors, contracts, contractor payments) →
P3 customers + payments + receivables →
P4 reporting + dashboards →
P5 polish (UX, theme, performance, audit, export).

## 11. Assumptions (ambiguities resolved)

1. Single company/tenant per deployment (no multi-tenant layer yet; `CompanyId` not modelled).
2. Frontend is React SPA (spec says "modern web application, API-first"); Blazor rejected for bundle size.
3. JWT access token (60 min) + refresh token (7 days) stored in `localStorage`; acceptable for internal tool.
4. Attachments abstracted behind `IFileStorage`; default `LocalFileStorage` under `App_Data/uploads`.
5. Currency is INR only; no FX. Amounts `decimal(18,2)`.
6. "SubOwner" approval rights default OFF; toggled per-permission by Owner.
7. Negative stock blocked by default; `Settings.AllowNegativeStock` per site to override.
8. Demo data seeded only when `Seed:Demo=true` and environment = Development; tagged `IsDemo=true` for easy purge.
