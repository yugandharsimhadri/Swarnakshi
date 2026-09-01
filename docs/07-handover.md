# 07 — Handover

Everything a new developer needs to pick up Swarnakshi. Read this, then
[01-architecture](01-architecture.md) and skim [05-progress](05-progress.md).

> **Status at handover (2026-09-01):** the 6-phase plan (P0–P5) is complete; the product is
> **multi-tenant SaaS** (registration, tenant isolation, EnterpriseAdmin console) and now carries an
> **employee/payroll** module. Read **[09-saas-tenancy](09-saas-tenancy.md)** before touching data access.
> `dotnet build` + `dotnet test` (186) + `npm run build` all green.
>
> **Picking this up?** Read §2 (run it), then walk the six use cases in **§6c** — twenty minutes,
> and you will recognise the code when you open it.

---

## 1. Where the project stands

| Area | State |
|------|-------|
| **Multi-tenancy** — `CompanyId` + global query filters + per-company unique indexes | ✅ done |
| **Company registration** (public) with per-tenant master-data provisioning | ✅ done |
| **EnterpriseAdmin console** — licence expiry, company-admin password reset, suspend | ✅ done |
| Auth (JWT + refresh), users, roles, fine-grained permissions | ✅ done |
| **User administration** UI (create, role, active, Sub-Owner permissions, Supervisor site scoping, password reset) | ✅ done |
| Sites (+ edit), projects (+ edit), all master data + Indian-construction seed | ✅ done |
| Inventory — weighted-average ledger, opening stock, adjustments, returns, negative-stock guard | ✅ done |
| Purchases → site inventory (landed-rate valuation) + supplier payments | ✅ done |
| Material requests → Owner approval → issue → consumption → project cost | ✅ done |
| **Reusable approval engine** (one pipeline, per-entity handlers, txn-scoped side effects) | ✅ done |
| Project expenses, labour (approved), cost-by-head, cost-by-type | ✅ done |
| **Employee master** (name / phone / salary / join date mandatory) + salary, advances and advance recovery | ✅ done |
| Contractors, contract works, contractor payments (approved, overpayment guard), ledger | ✅ done |
| Customers, receipts, receivables, ledger | ✅ done |
| Role-aware dashboard + 8 reports + CSV export | ✅ done |
| Audit log (status transitions), attachments (API + UI), optimistic concurrency | ✅ done |
| Skeleton loaders, confirm dialogs, light/dark theme, mobile-first shell | ✅ done |
| **Purchase delivered straight to a villa** (through stock, so totals reconcile) | ✅ done |
| Menu ordered by daily use: Home · Movement · Inventory · Projects · More | ✅ done |
| CI (GitHub Actions), **186 tests** (pure + SQLite-in-memory integration) + 24 browser UAT cases | ✅ done |
| **All 6 phases (P0–P5) complete** | ✅ |
| P6 nice-to-haves (simple-master admin UI, Scenario-B wiring, richer filters, transfers, …) | ⬜ backlog (§11) |

**Proof it works:** `dotnet test` — **186 green**, including `UseCaseWalkthroughTests` which asserts
the six business journeys in §6c. `dotnet build` and `cd web && npm run build` are clean.

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

Login: **`owner@swarnakshi` / `Owner@123`**  (logins are `username@companycode`)
Platform: **`EnterpriseAdmin` / `SivAyAAn@HMS`** — its own console, no company data.
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
    Entities/*.cs                 grouped by context: Platform (Company, PlatformUser), Identity, Masters,
                                  Sites, Inventory, Procurement, Expenses, Employees, Contractors,
                                  Customers, Approvals
    Entities/Inventory.cs         ← InventoryBalance.Receive()/Issue() = the weighted-average maths

  Swarnakshi.Application/        use-case services, DTOs, validators, interfaces. References Domain + EF Core (for IQueryable).
    Abstractions/                 IAppDbContext, ICurrentUser, IJwtTokenService, ITransactionSequenceService, IFileStorage, …
    Common/                       Result envelope, AppException, PageQuery/PagedResult, SettingsService,
                                  ProjectCostWriter  ← the ONLY writer of posted ProjectExpense rows
    Security/Permissions.cs       permission-key constants + role → default set
    Approvals/Approvals.cs        ApprovalService + IApprovalHandler + ApprovalEntityTypes
    <Context>/                    Auth, Platform (registration + EnterpriseAdmin), Sites, Projects, Masters,
                                  Inventory, Procurement, Expenses, Employees, Contractors, Customers,
                                  Dashboard, Reports, Attachments, Users
    DependencyInjection.cs        AddApplication() — register every service + approval handler here

  Swarnakshi.Infrastructure/     EF Core, config, migrations, seed, JWT, hashing, storage. Implements Application abstractions.
    Persistence/AppDbContext.cs   DbSets + OnModelCreating (decimal/string/DateTimeOffset conventions,
                                  ConcurrencyToken) + SaveChangesAsync (audit stamping, AuditLog, token bump)
    Persistence/Configurations/   one class per entity area — unique indexes, FK delete behaviour
    Persistence/Migrations/       InitialCreate → P5_ConcurrencyToken → P6_MaterialMaster →
                                  SaaS_MultiTenancy → SaaS_TokenRevocation → EmployeeMaster →
                                  PurchaseDirectToProject
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

tests/Swarnakshi.Tests/          xUnit + FluentAssertions (186). TestHost = real DI over SQLite in-memory,
                                 seeded exactly as a registered tenant is.
    UseCaseWalkthroughTests   ← the six business journeys of §6c; start here
    DirectToProjectPurchaseTests · MultiTenancyTests · EmployeeTests · InventoryBalanceTests
    CostFlowIntegrationTests · PaymentFlowTests · ConcurrencyTests · MaterialMasterTests
    PartyMasterTests · AuthAndUserTests · ExpenseAndApprovalTests · InventoryOperationsTests
    SiteReportingTests · AttachmentTests

tests/Swarnakshi.UatTests/       browser acceptance, 12 scenarios × 2 viewports. Gated: -p:Uat=true (§17)
tools/Swarnakshi.Automation/     the Playwright workflows those scenarios run

web/                             Vite + React 19 + TS + Tailwind v4 + Zustand + React Router 7
    src/lib/         api.ts (api + apiUpload + token refresh), types.ts, format.ts, useAsync.ts
    src/store/       auth.ts (Zustand), theme.ts
    src/components/  ui.tsx (kit), AppShell.tsx (bottom nav), SitePicker.tsx, AttachmentPanel.tsx
    src/pages/       Login, Register, PlatformConsole, Dashboard, Movement, Sites, Projects,
                     ProjectDetail (tabbed), Stock, Inventory, MaterialRequests, Purchases,
                     Approvals, Materials, Contractors, Customers, Employees,
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
10. **Tenancy is automatic — do not hand-roll it.** Query normally: the global filter scopes every
   read to the signed-in company. Insert normally: `SaveChangesAsync` stamps `CompanyId` and
   **throws** if no tenant is in scope. Only cross the filter deliberately, with
   `BeginTenantScope` or `IgnoreQueryFilters()`, and expect that to be questioned in review.
   New uniqueness rules go on `(CompanyId, …)`, never on the column alone.
11. **Optimistic concurrency is automatic** for `AuditableEntity` — `ConcurrencyToken` is
   regenerated in `SaveChangesAsync` and enforced as a concurrency token. Load entities
   *tracked* (`FirstOrDefaultAsync`, not `AsNoTracking`) in write paths so EF has the original
   token for the `WHERE` clause. A stale write surfaces as HTTP 409.
12. **Every PR updates `docs/05-progress.md`** in the same commit as the code.

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

## 6b. Worked use case — material bought for one villa

> *"I got 100 bags of cement for one villa. While entering it I want to send it straight to that
> villa — but it should still show in inventory so the totals match."*

This is supported directly: each **purchase line** has a destination — into site stock, or straight
to a villa on that site.

**What happens on post**, in one transaction:

```
Purchase line: 100 bags @ ₹450, deliver to Villa 101
   1. InventoryTransaction  PurchaseReceipt      +100 @ ₹450   (site stock)
   2. InventoryTransaction  ProjectConsumption   −100 @ ₹450   → Villa 101
   3. ProjectExpense        Material             ₹45,000       → Villa 101
```

**Worked example.** The store already holds 200 bags @ ₹400 (₹80,000).

| | Qty | Avg rate | Value |
|---|---|---|---|
| Store before | 200 | ₹400 | ₹80,000 |
| After receipt of 100 @ ₹450 | 300 | ₹416.67 | ₹125,000 |
| After issue of 100 @ ₹450 to Villa 101 | **200** | **₹400** | **₹80,000** |

Villa 101 is charged **₹45,000**. The store is left *exactly* as it was.

**Why the issue uses this purchase's landed rate, not the weighted average.** Two reasons, and they
agree:

1. It is what the buyer expects — the villa is charged what was actually paid for its material. At
   the blended ₹416.67 the villa would be charged ₹41,667 and the store would silently keep the
   ₹3,333 difference.
2. It is the only rate that leaves the pool undisturbed. Receiving *q* at *r* and issuing *q* at *r*
   restores quantity, value **and** average exactly, so material earmarked for one villa cannot
   distort the valuation of everybody else's stock.

**Why it goes through inventory at all** rather than straight to the project: the stock ledger stays
the single account of every movement, and the identity that protects the books survives —

```
total purchased  =  cost consumed by projects  +  value still on hand
₹125,000         =  ₹45,000                    +  ₹80,000            ✓
```

**Bulk or line by line** — the destination is per line, so one invoice can put the cement on a villa
and the steel into the store. Tax and discount are included: the villa bears the *landed* cost, not
the headline rate.

**Guard:** the target project must be on the same site as the purchase. Inventory is site-level, so
a project elsewhere cannot draw on that store; this is refused at entry as well as at post.

Covered by `DirectToProjectPurchaseTests` (6 tests), including the reconciliation identity above.

---

## 6c. The six named use cases — walk them, then change them

These are the journeys the business named. This section is written for someone picking the project
up: **run each one in the app first** (20 minutes for all six), then you will recognise the code when
you open it.

Start the app (§2), sign in as `owner@swarnakshi` / `Owner@123`. The dev seed gives you
site **Green Valley** with **Villa 101** and **Villa 102**, site **Sunrise Villas** with **Villa 103**,
customer **Ramesh Kumar**, supplier **Sri Balaji Traders**, and a 40-material catalogue including
**OPC 53 Grade Cement** (unit: BAG).

Run them in this order — 3 fills the store, 1 empties some of it, 2 is the special case.
To start clean at any point: stop the API, delete `src/Swarnakshi.Api/swarnakshi.db*`, run again.

---

### Use case 3 — Add cement bags to inventory

*Buying stock into the store. The foundation for everything else.*

**Walk it.** Movement → **Record a purchase** → Site `Green Valley`, Supplier `Sri Balaji Traders`,
remark "Lorry AP09 XX 1234" → line: `OPC 53 Grade Cement`, Qty `100`, Rate `400`, destination
**Into site stock** → **Save & post**.

Then Inventory → Green Valley.

| Expect | |
|---|---|
| Stock | 100 BAG @ ₹400 = ₹40,000 |
| Villa 101 material cost | **₹0** |

That zero is the point: **buying is not spending.** Money became inventory, not project cost. Add a
second delivery of 100 @ ₹450 and the store blends to 200 @ **₹425** — weighted average.

**Code** · `PurchaseService.CreateAsync` → `PurchasePoster.PostAsync` → `InventoryService.ReceiveAsync`
→ `InventoryBalance.Receive` (the actual arithmetic, in the Domain).
**Screens** · `web/src/pages/Purchases.tsx`, `web/src/pages/Inventory.tsx`.
**Tests** · `UseCaseWalkthroughTests.UseCase3_*`.

---

### Use case 1 — Move cement bags from inventory to a villa

*The daily loop: supervisor asks, Owner approves, store issues.*

**Walk it.** With 200 bags @ ₹425 in the store from above:

1. Movement → **Request material** → Project `Villa 101`, remark "First-floor slab",
   line `OPC 53 Grade Cement` qty `50` → **Submit for approval**.
2. More → **Approval Center** → the request is listed → **Approve** (read the confirmation: it says
   what is about to happen).
3. Back on the request → **Issue from stock** → confirm.

| Expect | |
|---|---|
| Store | 150 BAG, value ₹63,750 |
| Villa 101 material cost | **₹21,250** (50 × ₹425) |
| Ledger | a `Consumption` row, −50 @ ₹425, tagged Villa 101 |
| Identity | ₹21,250 + ₹63,750 = ₹85,000 purchased ✓ |

The issue rate is the store's **weighted average**, not any single delivery's price — the bags in a
pile are not labelled.

**Try to break it:** request 500 bags and approve it — the issue is refused, *Insufficient stock*
(unless `inventory.allow_negative_stock` is on for that site).

**Code** · `MaterialRequestService` → `MaterialRequestIssuer.IssueAsync` → `InventoryService.IssueAsync`
+ `ProjectCostWriter.WriteMaterialCostAsync`.
**Screens** · `web/src/pages/MaterialRequests.tsx`, `web/src/pages/Approvals.tsx`.
**Tests** · `UseCaseWalkthroughTests.UseCase1_*`.

---

### Use case 2 — Purchase cement bags direct to a villa

*"I got 100 bags for Villa 101 — it went straight there, but it must still show in inventory so the
totals match."* Full reasoning in **§6b**; this is how to see it.

**Walk it.** With 200 bags @ ₹400 in the store: Movement → **Record a purchase** → Green Valley,
Sri Balaji → line `OPC 53 Grade Cement`, Qty `100`, Rate `450`, destination **Straight to Villa 101**
→ **Save & post**.

| Expect | |
|---|---|
| Store, before | 200 BAG @ ₹400 = ₹80,000 |
| Store, after | **200 BAG @ ₹400 = ₹80,000** — unchanged |
| Villa 101 material cost | **₹45,000** (what was actually paid) |
| Ledger | `Purchase +100 @ ₹450` **and** `Consumption −100 @ ₹450 → Villa 101` |
| Identity | ₹45,000 + ₹80,000 = ₹1,25,000 purchased ✓ |

Both movements are recorded because the material goes **through** the store, not around it. The issue
uses this purchase's own landed rate, which charges the villa what was paid *and* leaves the pool's
average untouched — see §6b for why those two facts are the same fact.

The destination is **per line**, so one invoice can put cement on the villa and steel into the store.
Tax and discount are included: the villa bears the landed cost, not the headline rate.

**Try to break it:** pick `Villa 103` (on Sunrise Villas) as the destination for a Green Valley
purchase — refused, because inventory is site-level.

**Code** · `PurchaseItem.DeliverToProjectId` → `PurchasePoster.DeliverToProjectAsync`.
**Screens** · the destination `<Select>` on each line in `web/src/pages/Purchases.tsx`.
**Tests** · `DirectToProjectPurchaseTests` (6) + `UseCaseWalkthroughTests.UseCase2_*`.

---

### Use case 4 — Approval for every purchase and every stock movement

*Nothing moves and no money posts until the Owner says so.*

**Material movement is always gated.** Create a request and try **Issue** before submitting — refused,
*must be approved*. Submit it, try again while it is still pending — refused again, and the store is
still untouched. Approve, then issue — now it moves. Reject one instead and the stock never moves.

**Purchases are gated by a setting**, off by default so a small builder is not slowed down. There is
no settings screen yet, so flip the row directly:

```sql
-- src/Swarnakshi.Api/swarnakshi.db
UPDATE Settings SET Value = 'true' WHERE "Key" = 'purchase.needs_approval';
```

Restart the API, and a submitted purchase now sits at `PendingApproval` with **nothing entering the
store** until the Owner approves it. (A settings screen is on the backlog — §11.)

Everything approvable runs through one engine — see **§5** for how to add another. Money-out already
on it: contractor payments, labour, employee salary/advances.

| Setting | Default | Effect |
|---|---|---|
| `purchase.needs_approval` | `false` | purchases post straight to stock |
| `inventory.adjustment_needs_approval` | `true` | adjustments are Owner-only |
| `inventory.allow_negative_stock` | `false` | issues cannot overdraw the store |

**Code** · `ApprovalService.DecideAsync` runs the entity's `IApprovalHandler` **inside one DB
transaction** — if a side effect throws, everything rolls back and the request stays pending.
**Tests** · `UseCaseWalkthroughTests.UseCase4_*` pins the gate from three sides: before submit, while
pending, and after rejection.

---

### Use case 5 — Customer payments

*What the villa is worth, what has come in, what is still owed.*

**Walk it.** Projects → `Villa 101` → **Customer** tab. Sale value ₹80,00,000 (from the seed).
**Record receipt** → ₹10,00,000, Bank Transfer, reference "NEFT-8891", remark "First instalment".
Repeat for ₹15,00,000.

| Expect | |
|---|---|
| Received | ₹25,00,000 |
| Outstanding | ₹55,00,000 |
| Customer ledger | More → Customers → Ramesh Kumar — same figures across all his projects |

Receipts post immediately — money **in** does not need approval; money **out** does.
A receipt on a project with no customer is refused (self-owned builds are legitimate, so the customer
is optional on a project — but you cannot receive from nobody).

**Note the asymmetry:** customer receipts are revenue and never touch project *cost*. Margin is
`sale value − total cost`, which is why receiving money does not make a villa look more profitable.

**Code** · `CustomerPaymentService`. **Screen** · Customer tab in `web/src/pages/ProjectDetail.tsx`.
**Tests** · `UseCaseWalkthroughTests.UseCase5_*`.

---

### Use case 6 — Data entry stays simple, and carries remarks

*Two rules that pull against each other, both of which matter on site.*

**Simple.** A purchase needs only supplier, site, material, quantity and rate. Invoice number, tax,
discount, delivery destination and remarks are all optional. A material request needs a project, a
material and a quantity. Nothing else is compulsory anywhere in the daily loop.

**With remarks.** Every daily entry carries a free-text note, and it is on the form, not just the API:

| Entry | Field | What people actually write |
|---|---|---|
| Purchase | Remarks | "Lorry AP09 XX 1234, received by store keeper" |
| Material request | Remarks | "First-floor slab" |
| Customer receipt | Remarks | "Part payment, cheque handed to site office" |
| Opening stock / adjustment | Remarks / Reason | "Counted at handover", "Damaged in unloading" |
| Employee payment | Remarks | "Festival advance" |

A number with no note is a number nobody can explain three months later. When you add a new entry
screen, add the remark field — it is the cheapest thing in the system and the first thing asked for.

**Tests** · `UseCaseWalkthroughTests.UseCase6_*` asserts each of those round-trips, and that a
purchase can be recorded with nothing optional filled in.

---

### Where these six can go next

Honest edges, so nobody assumes they are finished:

- **Partial issue** works in the service (`IssueRequest.Items` takes per-line quantities) but the UI
  only issues everything approved. Wiring the per-line boxes is a small, self-contained job.
- **Returns from a villa** exist in the API (`POST /api/inventory/returns`, reverses the project cost)
  with no screen.
- **Opening stock and adjustments** are API-only — no UI at all yet.
- **Scenario B** — a `RequestType = Purchase` request that auto-creates the linked PO on approval —
  is modelled (`PurchaseHeader.MaterialRequestId`) but not wired.
- **Customer receipts post directly.** If a company wants them approved, add an `IApprovalHandler`
  (§5); the pattern is already there in `ContractorPayment`.

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

**The tab bar is ordered by how often a screen is opened, not by data hierarchy:**
`Home · Movement · Inventory · Projects · More`. Movement is the daily loop — request material,
approve, issue, record a purchase, record spend. Sites, material master, contractors, customers,
employees and reports are set-up-or-review work and live under **More**. Resist promoting a screen to
the tab bar because it feels important; promote it because it is opened daily.


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

The six business journeys are walked by hand in **§6c** and asserted in `UseCaseWalkthroughTests`.
Start there when a change touches stock, cost or approvals.

`tests/Swarnakshi.Tests` — **186 tests across 15 classes**, all services covered:

| Area | Classes |
|---|---|
| Masters | `PartyMasterTests` (28), `MaterialMasterTests` (23), `EmployeeTests` (13) |
| Security & tenancy | `AuthAndUserTests` (20), `MultiTenancyTests` (18) |
| Stock & cost | `InventoryOperationsTests` (12), `InventoryBalanceTests` (5), `CostFlowIntegrationTests` (2), `DirectToProjectPurchaseTests` (6) |
| Money & approval | `ExpenseAndApprovalTests` (12), `PaymentFlowTests` (3) |
| Reporting & misc | `SiteReportingTests` (13), `AttachmentTests` (5), `ConcurrencyTests` (1) |
| The named journeys | `UseCaseWalkthroughTests` (12) — the six use cases of §6c |

`TestHost.CreateAsync()` builds the real DI graph (`AddApplication()` + SQLite in-memory + seeded
masters) and a `FakeCurrentUser` with all permissions. Copy it for any new integration test — and
pass the registered `CurrentUser` through, rather than newing up a second one, or permission
assertions pass against a user the code under test cannot see.

Manual e2e (need the API running on :6051): `node scratchpad/p1test.mjs` … `p4test.mjs`.
These are throwaway scripts in the git-ignored scratchpad — they exercise the full flow per phase
(P1 inventory, P2 expenses/contractors, P3 customers, P4 dashboard/reports). Recreate from the
patterns if lost, or promote them to xUnit.

CI (`.github/workflows/ci.yml`) runs three jobs on every push/PR to `main`: `dotnet build+test`
(the 186 fast tests), `npm run build`, and the browser UAT suite as its own job. UAT is separate
because it is gated out of `dotnet test` — without a dedicated job the acceptance journeys can go
completely red while CI stays green, which is exactly what happened when multi-tenant sign-in
landed.

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
| Employees (`/employees`) | `/api/employees` (+ `/{id}/ledger`), `/api/employee-payments` (+ `/submit` `/cancel`) | master `masters.manage` · pay `labour.create` |
| Users (`/users`) | `/api/users` + `/permission-keys` `/{id}` `/password` `/permissions` `/sites` | `users.manage` |
| More (`/more`) | — theme, logout, links | all |
| (any detail) Documents panel | `/api/attachments` (+ `/{id}/download` `/{id}` DELETE) | entity's edit permission |

**Role → default permissions** (in `Permissions.ForRole`): Owner = all · Sub-Owner =
`inventory.view` + `reports.view` (extend per-user in the Users screen) · Supervisor =
`inventory.view`, `material_request.create`, `purchase.create`, `projects.manage`, `reports.view` ·
Accountant = `expense.create`, `labour.create`, `contract.manage`, `contractor_payment.create`,
`customer_payment.create`, `inventory.view`, `reports.view`.

## 15 — Material Master (P6)

Redesigned end-to-end against `Swarnakshi_Material_Master_50_Categories.xlsx`.

| Piece | Where |
|---|---|
| Taxonomy (50 categories, subcategories, spec fields) | `Infrastructure/Persistence/Seed/MaterialTaxonomy.cs` |
| Idempotent seeding + legacy remap | `Infrastructure/Persistence/Seed/MaterialMasterSeeder.cs` |
| Service (CRUD, lifecycle, duplicate + code-lock rules) | `Application/Masters/MaterialService.cs` |
| Signature / summary rules (shared) | `Application/Masters/MaterialIdentity.cs` |
| API | `Api/Controllers/MastersController.cs` → `MaterialsController` |
| UI | `web/src/pages/Materials.tsx` |
| Tests | `tests/Swarnakshi.Tests/MaterialMasterTests.cs` |

Rules that are enforced **server-side**, not just in React:
- Duplicate identity = Name + Brand + identity specs, normalised into `SpecSignature` with a
  unique index. 409 on collision.
- Material Code becomes immutable once any PurchaseItem / MaterialRequestItem /
  InventoryTransaction / InventoryBalance references the material. 409 on change.
- Deactivation is refused while stock exists at any site. 409, with the message the UI shows.
- No DELETE endpoint. Lifecycle is Active ↔ Inactive so history survives.
- Writes require `masters.manage` (Owner). Supervisor and Accountant are read-only.

Specification fields are declared per subcategory by `MaterialSpecDefinition` and fetched by the
form from `/api/materials/spec-definitions?subcategoryId=`. Company/Brand is a Material column, not
a spec. Material never stores stock — the detail view reads it from `/api/materials/{id}/stock`.

Gotchas 18-20:
18. EF maps `string.Contains` to SQLite's case-sensitive `instr()` — search lowercases both sides.
19. `BaseEntity` pre-sets `Id`, so adding a child through a tracked parent's navigation marks it
    `Modified`; add spec values via `db.MaterialSpecValues.Add(...)`.
20. Any migration adding a NOT NULL + unique column must backfill before the index is created.

## 16 — Contractor & Customer master (P7)

| Piece | Where |
|---|---|
| Service (all three party kinds) | `Application/Masters/PartyService.cs` |
| API | `Api/Controllers/MastersController.cs` → `PartiesController` |
| UI (shared by both screens) | `web/src/components/PartyMaster.tsx` |
| Screen configs | `web/src/pages/Contractors.tsx`, `Customers.tsx` |
| Tests | `tests/Swarnakshi.Tests/PartyMasterTests.cs` |

Contractor, Customer and Supplier are one implementation keyed by `PartyKind` — do not fork it.

Enforced **server-side**:
- Code unique per kind; 409 on collision. Names are NOT unique — two contractors may share one.
- Code immutable once any contract / contractor payment / project / customer payment / purchase
  references the record.
- Deactivation is always permitted (no stock equivalent) and never modifies historical rows.
- Inactive parties are rejected for new contracts (`ContractService`), contractor payments and new
  projects (`ProjectService`). `CustomerPayment` inherits the guard via its project.
- Writes require `masters.manage`; Supervisor and Accountant are read-only.
- Create/update/deactivate/reactivate each write an `AuditLog` row.

Gotcha 21: EF cannot translate `Where`/`Any` applied on top of a positional-record projection —
it throws at runtime. Filter and order on the concrete entity and project last.

## 17 — UAT suite

`tools/Swarnakshi.Automation` (Playwright library, scenarios as `IWorkflow`) +
`tests/Swarnakshi.UatTests` (xUnit, one class per module, both viewports). Full detail in
[08-uat.md](08-uat.md).

```bash
dotnet test tests/Swarnakshi.UatTests -p:Uat=true
```

**The switch is required.** The suite is excluded from a bare `dotnet test` (`IsTestProject` is
gated on `Uat`) so the root run stays fast — it starts servers and drives a browser for minutes.
Without the switch it reports no tests and exits 0, which reads as a pass. If a UAT run says "no
tests", that is what happened.

Gotchas 22-24:
22. `ASPNETCORE_URLS` does NOT beat `"Urls"` in appsettings.Development.json —
    `WebApplication.CreateBuilder` layers app config over host config. Pass `--urls` as an
    application argument instead, or the UAT API binds the developer's 6051.
23. Search boxes and filter rows are bare inputs/selects with no `Field` wrapper, so `GetByLabel`
    finds nothing; use placeholder and `select:has(option…)` locators.
24. Nav links and More-page cards include an icon or chevron in their accessible name ("☰ More"),
    so an exact name match never matches. Locators are substring by default for this reason.

Gotcha 25 — **never name a paged action parameter `page`.** Bind it as
`[FromQuery] PageQuery paging`. ASP.NET binds a complex type under the parameter name as a prefix,
so a request carrying `?page=1` makes the binder look for `page.q` / `page.pageSize`, find nothing,
and hand the action an empty PageQuery — search and page size are dropped with no error. This was
live on all 13 list endpoints and made every search box in the product inert.

Gotcha 26 — `useAsync` applies only the newest request's response (monotonic counter in
`web/src/lib/useAsync.ts`). Keep that guard: without it a slow early request overwrites a newer one
and lists flick back to stale or empty rows, which is invisible in manual testing and shows up as
flaky UAT.

Gotcha 27 — **the bottom tabs are a product decision the suite encodes.** `TabBarLabels` in
`WorkflowContext` lists what is reachable in one tap; anything else is reached through More. When
the menu was reordered by daily use (Sites and Stock demoted, Movement and Inventory promoted),
twelve cases failed on a tab that no longer existed. Update that list when the menu changes.

Gotcha 28 — **a login is `username@companycode`, not an email.** Since multi-tenancy it is resolved
against a company rather than a global user table, and the founding admin's display name is the
*company* name, because that is what `PlatformSeeder` writes into `User.Name`. The UAT's copy of all
three lives in `DemoData`, so a seed change is one edit rather than a hunt through the workflows.

Gotcha 29 — **populate a column before building a UNIQUE index over it.** Twice now a migration has
added a column with a constant default and then indexed it uniquely: `SpecSignature` in P6, and
`Users.Username` in SaaS_MultiTenancy. Both apply cleanly to an empty database and fail on a real
one — the second blocked the upgrade for any company with more than one user. If a migration adds a
column that is part of a unique index, it must write distinct values first, portably.

Gotcha 30 — **the upgrade path is not the seeded path.** `PlatformSeeder` creates its founding owner
only when there are no users, so on an upgraded database that branch never runs. Anything that
assumes a freshly seeded tenant needs a matching answer for a tenant adopted from a
pre-multi-tenancy database — see `UpgradeFromSingleTenantTests`.
