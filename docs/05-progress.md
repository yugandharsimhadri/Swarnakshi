# 05 — Progress Log

Newest first. Every PR appends an entry: date, area, what changed, what's next, gotchas.

---

## 2026-09-01 — Employee master, salary and advances

**Why a second people concept.** `LabourEntry` records daily site labour as a cost by category with
no worker master — which is how gangs are actually engaged. `Employee` is the small number of named
people on monthly salary who take advances against it. Neither replaces the other.

**Domain** — `Employee` (Code, Name, Phone, MonthlySalary, JoinDate mandatory; Designation, Address,
home Site, LeaveDate optional) and `EmployeePayment` (Salary / Advance / Bonus / Reimbursement,
`AdvanceRecovered`, optional salary period, optional project). Migration `EmployeeMaster`.

**Advance arithmetic** — `outstanding = advances given − advances recovered`, derived from posted
payments rather than stored, so it cannot drift from the ledger that produced it. Recovering more
than is outstanding, or more than the payment itself, is refused; an advance cannot recover an advance.

**Money-out follows the existing rule** — draft → submit → Owner approval → posted, through the same
approval engine as contractor and labour payments. A salary run should not be the one way to move
cash unreviewed.

**Project attribution is opt-in.** A payment charged to a project posts `ProjectExpense(Labour)` at
the **gross** amount — recovering an advance is the employee repaying the company, not a discount on
what the month cost. Left unassigned it stays a company overhead and never touches project cost.

**Permissions reuse** — the employee record is master data (`masters.manage`); payments are labour
cost (`labour.create`, which the Accountant already holds). No new keys, so the role matrix is unchanged.

**Frontend** — Employees screen (More → Employees) with payroll/advance KPIs, phone-searchable list,
add/edit sheet marking the four mandatory fields, a Pay sheet that shows net-after-recovery live, and
a per-employee ledger.

**Verified** — 15 new tests (168 total, all green) + the full advance→salary→recovery cycle driven
through the live API and checked in the browser.

---

## 2026-09-01 — SaaS: multi-tenancy, company registration, EnterpriseAdmin

Swarnakshi becomes SaaS. It is now **one customer among many** — a company with several sites —
and any builder can register their own. Full detail in **[09-saas-tenancy](09-saas-tenancy.md)**.

**Domain**
- `Company` (tenant) and `PlatformUser` (EnterpriseAdmin) inherit a new `PlatformEntity` — above tenancy.
- `BaseEntity` gains `CompanyId` via `ITenantOwned`; `User` gains `Username`, `IsCompanyAdmin`,
  `TokensValidFrom`; `Email` becomes optional contact rather than the login.

**Isolation** (three mechanisms, none of which a new feature can forget)
- Global query filter on every `ITenantOwned` entity, applied by reflection so a new entity is
  isolated the moment it joins the model. Null tenant filters to nothing — fail-safe.
- Insert stamp in `SaveChangesAsync`; a write with no tenant in scope **throws** rather than
  writing an orphan row.
- Every unique index is now composite on `(CompanyId, …)` — codes, `SpecSignature`, every
  `TxnNumber`, and `TransactionSequence`, so each company numbers documents from 00001.
- `IAppDbContext.BeginTenantScope(companyId)` for the deliberate crossings (login, registration
  seeding, the platform console).

**Identity** — `username@companycode`. Usernames unique per company; company codes globally unique;
company names free to repeat. One login box: no `@` means a platform operator.

**Registration** — public `POST /api/register`, 30-day trial, provisions the tenant with its own
units / 50-category taxonomy / expense heads / labour categories / payment methods / settings.

**EnterpriseAdmin** (`EnterpriseAdmin` / `SivAyAAn@HMS`) — licence expiry and company-admin password
resets, and nothing else. Structurally barred from company data: no CompanyId, so the filters
exclude it. Its own console screen.

**Gates** — `[TenantOnly]` / `[PlatformOnly]` on every controller: 403 for the wrong token kind or a
suspended company, 402 for an expired licence, 401 for a token predating a password reset.

**Frontend** — login by `username@companycode` with live company-code availability on a new
registration screen; Enterprise console (licence extend / set / suspend / reset password); licence
banner from 14 days out; Users screen switched to usernames.

**Upgrade path** — `PlatformSeeder` creates the founding `swarnakshi` company and adopts every
pre-tenancy row into it, so an existing database keeps its business. Seeded owner: `owner@swarnakshi`.

**Verified** — 153 unit/integration tests (21 new in `MultiTenancyTests`) + 25 live-API checks in
`scratchpad/saastest.mjs`; registration, isolation, console and licence flows checked in the browser.

**Two real defects this work surfaced and fixed**
1. `AppDbContext` snapshotted the tenant in its constructor. Correct per-request in production, but
   it froze whatever identity existed when the context was first resolved — nobody, if anything
   touched it before authentication. Now a property resolved per query.
2. A password reset cleared the refresh token but left the live access token working for the rest of
   its hour — the exact hour a compromised session would use. `TokensValidFrom` vs the `swk_iat`
   claim now kills it immediately.

**Gotchas**
- Query filters must close over a context **property/field**, and the model is cached — build the
  filter through a generic helper so EF resolves it against the executing instance.
- `IgnoreQueryFilters()` is required for any legitimately cross-tenant read (login by refresh token,
  the platform console) — and should be rare enough to notice in review.
- The UAT suite launches the API with `-c Debug --no-build`; running `dotnet test` for it in
  Release fails all cases with "cannot find Swarnakshi.Api.exe".

---

## 2026-09-01 — A stale `reload` could make a list contradict its own filters

**Product bug, found by the mobile UAT run.** A mutation handler closes over `reload` at the moment
the row action is clicked, then awaits its POST. If a filter changed while that request was in
flight, the refresh afterwards re-queried with the filters *as they were at click time* — and being
the newest request, it won the ordering guard and replaced what the user was looking at. The screen
then contradicted itself: Status read "All" while the rows were the Active-only ones.

Deactivating a contractor and immediately widening the status filter reproduced it every time on
mobile, which is slow enough to lose the race; desktop won it and passed.

`useAsync` now reads `fn` through a ref, so `reload` always runs the *current* query — which is what
every caller already assumed it meant.

`PartyMasterTests` gains the regression that isolated it: deactivate, then list by code with the
status filter unset. That passing is what proved the query was sound and sent me to the client.

**Also:** a bare `dotnet test` at the root was running the browser UAT suite — 2 minutes and a
browser dependency on everyone's test run — despite a csproj comment claiming otherwise. The
`IsUatSuite` property that comment referred to was inert and read by nothing. It is now gated on
`IsTestProject`, so the root run is the fast suite again:

```
dotnet test                                        # 132 unit + integration, ~35s
dotnet test tests/Swarnakshi.UatTests -p:Uat=true   # 24 browser cases, ~1m30s
```

The project stays in the solution, so `dotnet build` still compiles it and it cannot rot unnoticed.

**Gotchas**
- Widen a status filter *before* typing a search, not after: changing a filter re-queries with
  whatever the debounced term is at that instant, so the two settle independently.
- If a UAT run reports "no tests", the `-p:Uat=true` switch is missing.

---

## 2026-09-01 — UAT suite green: 24/24 in both viewports

The browser-driven acceptance suite (`tests/Swarnakshi.UatTests` + `tools/Swarnakshi.Automation`)
now passes every scenario on desktop and mobile. See [08-uat](08-uat.md).

**The last three failures were all one bug in the harness.** Both layouts ship in the DOM at once,
and `TableWrap` renders `div.rounded-2xl` — the same class as the mobile `Card` — carrying every
row's text. On mobile the row lookup therefore bound to the *hidden desktop table* and waited out
its timeout for a button that could never become visible. Visibility was being filtered on the
control but not on the container that held it. Row resolution now lives in one place
(`WorkflowContext.Row`), which filters the container, and `RowAction` / `ExpectRowStatusAsync` /
`OpenDetailAsync` all go through it.

Also added `ExpectRowStatusAsync`, so a lifecycle step waits for the row to actually read `Inactive`
rather than for the network to go quiet — it asserts the business outcome and synchronises on it in
one move. A bare network wait had been letting runs continue against a list that still showed the
old state.

**Next**
- Wire the UAT suite into CI as its own step (it takes ~2 min and starts servers, so not in the
  default `dotnet test`).

**Gotchas**
- Don't pipe a UAT run into `tail`/`head` — they buffer until exit, so a run in flight looks silent.
  Redirect to a file instead.
- Assertion timeouts are separate from page timeouts; `Assertions.SetDefaultExpectTimeout` is set
  explicitly in the fixture.

---

## 2026-08-31 — Stale responses could overwrite any list (found by UAT)

`useAsync` had no request-ordering guard: it applied whichever response resolved last, regardless of
which request it belonged to. Every list in the app re-queries on each keystroke and each filter
change, so several requests are routinely in flight — and a slow early one would overwrite the
results the user was actually looking at, flicking the list back to stale or empty rows a moment
after showing the right ones.

Fixed with a monotonic request counter: only the most recent request may write state, and unmount
supersedes anything in flight (which also removes the late-setState-after-unmount path).

This is what made the Material Master lifecycle scenario fail intermittently — the row was found
under the Inactive filter, then vanished mid-step when an older response landed.

---

## 2026-08-31 — Search was broken on every list endpoint (found by UAT)

Chasing the last UAT failures turned up a real product bug, not a test bug: **`q` and `pageSize`
were silently ignored on every paged list in the app** whenever the client sent `page`.

```
GET /api/materials?q=UAT-LIF-DIAG              -> total 1   correct
GET /api/materials?q=UAT-LIF-DIAG&pageSize=50  -> total 1   correct
GET /api/materials?q=UAT-LIF-DIAG&page=1       -> total 42  q dropped
```

**Cause.** The action parameter was named `page` (`[FromQuery] PageQuery page`). ASP.NET binds a
complex type by first looking for values under the parameter name as a prefix; a request carrying
`?page=1` matches that prefix, so the binder switches to prefixed mode, looks for `page.q` /
`page.pageSize` / `page.sort`, finds none, and hands the action an empty `PageQuery`. No error — the
endpoint just answers with the unfiltered first page.

**Reach.** 13 endpoints across 9 controllers: materials, contractors/customers/suppliers, projects,
sites, purchases, material requests, inventory ledger, approvals, expenses, contracts and customer
payments. The frontend always sends `page`, so **every search box in the product was inert** —
typing filtered nothing; the list simply re-rendered unfiltered. Several UAT scenarios had been
passing for the wrong reason, "finding" a record only because the whole list came back.

**Fix.** Renamed the parameter to `paging` everywhere, so no query key matches the prefix and the
binder falls back to unprefixed binding. `PageQuery` now carries a comment stating the rule.

---

## 2026-08-31 — UAT suite (Playwright, browser-driven acceptance)

A frontend acceptance layer above the unit suite: it starts the API and the Vite client, signs in as
the seeded owner, and performs the business journeys in a real browser. Modelled on the UAT projects
in `TransTruck_Web` (`tests/TransTrack.UatTests` + `tools/TransTrack.Automation`) and `HMS_WEB`.

**Layout**
- `tools/Swarnakshi.Automation` — Playwright library: server management, browser session, and the
  scenarios themselves as `IWorkflow` objects.
- `tests/Swarnakshi.UatTests` — xUnit suite, one class per module, each running one workflow in
  **both viewports** via a `[Theory]`.

Scenarios live in the automation library, not the test project, so the same objects can be replayed
headed with captions (`SWARNAKSHI_UAT_RUN_MODE=demo`) — what is demonstrated and what is signed off
are the same journey by construction.

**12 scenarios**: SignIn, Dashboard, UserAccess, MaterialCatalogue, AddMaterial, MaterialLifecycle,
ContractorMaster, CustomerMaster, PurchaseToConsumption, MaterialRequestApproval, SiteInventory,
Reports. Documented in [08-uat.md](08-uat.md).

**Status: 21 of 24 cases pass.** `MaterialLifecycle` (both viewports) and `ContractorMaster`
(mobile) remain open — see 08-uat.md for the evidence and the next step.

**Isolation** — runs on 6070/6071, never the developer's 6050/6051, against a throwaway SQLite file
under `artifacts/uat/` that is deleted afterwards. `web/vite.config.ts` now reads
`SWARNAKSHI_WEB_PORT` / `SWARNAKSHI_API_URL` (defaults unchanged) so the client's proxy can be
pointed at the run's own API.

**A product gap the UAT found:** no seeder creates a **Supplier**, and there is no supplier
management screen — so on a fresh install a purchase cannot be recorded through the UI at all, since
the supplier picker is empty. A demo supplier (`SUP-001 Sri Balaji Traders`) now joins the
Development-only demo seed alongside the demo sites and customer. A supplier master screen remains
genuinely missing; the party service already supports suppliers, so only the UI is absent.

**Gotchas**
- `ASPNETCORE_URLS` does **not** override `"Urls"` in appsettings.Development.json:
  `WebApplication.CreateBuilder` layers application configuration over host configuration, so the
  JSON wins. The first run therefore bound the API to the developer's 6051 — precisely the collision
  the design exists to prevent — and then timed out waiting on 6071. Fixed by passing `--urls` as an
  application argument (command line is the last provider). The timeout now says so explicitly.
- The search boxes and filter selects are bare inputs/selects with no `Field` wrapper, so
  `GetByLabel` finds nothing for them; they need placeholder and `select:has(option…)` locators.
- Every master screen renders BOTH layouts into the DOM (`hidden lg:block` table, `lg:hidden`
  cards), so every name and button exists twice. Locators must filter to visible before `.First`,
  or they bind to the hidden copy and wait out the timeout.
- The `Field` component puts the control **inside** the `<label>`, so an implicit label's accessible
  name includes the control's own text — for a `<select>`, every option. `GetByLabel("Category *",
  exact)` therefore can never match a select. Match the caption span
  (`label:has(> span:text-is(…))`) and reach into it instead.
- Confirm dialogs reuse the verb of the row action that opened them, so an unscoped
  `GetByRole(Button, "Deactivate")` finds the row button now sitting behind the overlay and fails as
  "not stable". Scope confirmation clicks to `role=dialog`.
- Signing in returns the user to the route they were on, not to the dashboard.
- Select options arrive with the API response, so reading a select immediately after navigation
  finds only the empty prompt — which reads as unseeded data rather than a race.

---

## 2026-08-31 — Test coverage audit: 61 -> 131 tests

Audited coverage by mapping every Application service against the suite. Five services were
completely untested, including the two most security-sensitive ones. Backend code was not changed
except where the tests exposed defects (below).

**New suites**
- `AuthAndUserTests` (20) — login success/failure, identical error for bad password vs unknown email
  (no account enumeration), deactivated user blocked, password hashing, refresh-token rotation with
  old-token reuse rejected, logout revocation, user CRUD, self-role-change and last-Owner guards,
  password reset, SubOwner extra permissions, and a `[Theory]` locking the role→permission map.
- `InventoryOperationsTests` (12) — opening stock, positive/negative adjustments, the
  approvals-permission guard on adjustments, return-from-project reversing material cost,
  cross-site rejection, site-scoped balances, ledger filtering.
- `ExpenseAndApprovalTests` (12) — direct expenses posting to project cost, cancellation reversing
  it while keeping the row, head/subhead validation, the approval **reject** path leaving stock and
  project cost untouched, and the simple-master "in use" delete guard.
- `SiteReportingTests` (13) — site CRUD/filtering, lookup masters, dashboard KPIs asserted against a
  known purchase, all 8 reports well-formed, site-scoped stock report, low-stock threshold.
- `AttachmentTests` (5) — upload/download round-trip, entity scoping, delete.

**Two defects found in the test infrastructure itself**
- `TestHost` never registered `IJwtTokenService`, so the entire auth layer was **untestable by
  construction** — the first auth test failed with a DI resolution error, not an assertion.
- `TestHost.CurrentUser` was a *second* `FakeCurrentUser` instance, unrelated to the one in DI, so
  its `UserId` was always null and mutating it did nothing. Now exposes the registered instance,
  which is what lets tests switch role/permissions mid-test.
`IFileStorage` was also added (throwaway temp folder, cleaned on dispose) so attachments are testable.

**Coverage now**: every Application service is exercised. Suite run twice back-to-back — 131/131
both times, no ordering flakiness.

---

## 2026-08-31 — P7: Contractor & Customer master management

Both screens previously exposed only "New". They are now full master-management screens, matching
the Material Master pattern. No accounting, project-cost or payment logic was touched.

**Application** — new `PartyService` handles contractors, customers *and* suppliers through the
existing `PartyKind` enum, so the three stay one implementation. Party logic moved out of
`MasterService`. Adds list/search/filter/summary, contractor-type lookup, detail with usage counts,
code locking, deactivate/reactivate and explicit `AuditLog` writes.

**API** — `/api/{contractors|customers|suppliers}` gains `summary`, `types`, `{id}`, `deactivate`,
`reactivate`. Still no DELETE. `SavePartyRequest` no longer carries `IsActive`: creation is always
Active and status changes only through the lifecycle endpoints, so every change is audited.

**Frontend** — one `PartyMaster` component drives both screens; `Contractors.tsx` and
`Customers.tsx` are ~20-line configs. Summary cards, debounced server-side search, Status filter
defaulting to **Active**, contractor-type filter, desktop table + mobile cards, and
Add/Edit/View sheets with the six-section form.

**Validation** — PAN, GSTIN and mobile formats added to `SavePartyValidator` alongside the existing
code/name/email rules. Names are deliberately *not* unique: two contractors may share a name.

**Data** — no schema change and no migration; every field already existed. The one user-created
contractor and the demo customer keep their Ids, codes and history.

**Tests** — 61 pass (38 pre-existing + 23 new in `PartyMasterTests`). Regression verified live over
HTTP: contractor → contract → contractor payment, and customer → project → customer payment, both
surviving deactivate/reactivate with names still resolving; inactive parties rejected with 400 on
new contracts and new projects.

**Gotchas**
- EF cannot translate `Where`/`Any` applied *on top of* a positional-record projection — it fails at
  runtime, not compile time. Filter and order on the concrete entity, project last. This broke the
  duplicate-code check and every list/filter/summary query before it was fixed.

---

## 2026-08-31 — P6: Material Master redesign (end-to-end)

Complete redesign of the Material Master against the approved business reference
(`Swarnakshi_Material_Master_50_Categories.xlsx`). No change to inventory valuation, procurement,
material requests, consumption or project costing.

**Domain**
- `Material` gains `Brand`, `GenericMeasurement`, `SpecSummary`, `SpecSignature` (unique).
- New `MaterialSpecDefinition` (per subcategory) + `MaterialSpecValue` (per material), `SpecFieldKind`.
- `MaterialIdentity` (Application) is the single definition of the duplicate signature and the
  display summary — shared by the service and the seeder so both produce identical keys.

**Taxonomy** — 50 categories / 207 active subcategories / 222 spec definitions, in
`MaterialTaxonomy`. Sand ≠ Aggregates, Bricks ≠ Blocks, Granite ≠ Tiles, Waterproofing Materials is
its own category. Owner-approved departures from the Excel: Tiles classified by **body type**
(Vitrified / Ceramic / Mosaic / Cement-Terrazzo / Clay-Terracotta) and Granite/Marble by **form**,
not by application — the Excel's location axis would force duplicate SKUs. Excel's Tiles `Type`
spec is redefined as optional, non-identifying `Application`.

**Migration & data preservation** — `MaterialMasterSeeder` is idempotent and remaps the legacy
19-category tree. All 40 seeded materials kept their `Id` and `Code`; only
`MaterialSubcategoryId` was repointed, so every InventoryBalance / InventoryTransaction /
PurchaseItem / MaterialRequestItem still resolves. Retired categories are deactivated, never
deleted. `MAT-PLB-VAL` renamed to "Brass Ball Valve" (owner-approved).

**API** — `/api/materials` gains summary, brands, spec-definitions, `{id}/stock`, deactivate and
reactivate. No DELETE. Material logic moved out of `MasterService` into `MaterialService`.

**Frontend** — `Materials.tsx` rewritten: summary cards, server-side search, five filters,
desktop table + mobile cards, and Add/Edit/View with subcategory-driven specification fields.

**Tests** — 34 pass (11 pre-existing + 23 new). Cost-flow invariant re-verified end to end over
HTTP: 100@400 + 100@450 → avg 425; issue 50 → MaterialCost 21,250 + inventory 63,750 = 85,000.

**Gotchas**
- EF maps `string.Contains` to SQLite's **case-sensitive** `instr()`, so `q=cement` silently missed
  "OPC 53 Grade Cement". Both sides are now lowered via `ToLower()` (portable to SQL Server).
  Regression test: `Search_is_case_insensitive`.
- `BaseEntity` pre-populates `Id`, so a child added only through a **tracked** parent's navigation
  is classified `Modified` and EF emits an UPDATE for a row that does not exist. Add spec values
  through `db.MaterialSpecValues.Add(...)` on an existing material.
- The generated migration defaulted `SpecSignature` to `""` for every existing row and then built a
  **unique** index — guaranteed failure with any data. A portable
  `UPDATE Materials SET SpecSignature = CAST(Id AS varchar(64))` was inserted before the index; the
  seeder replaces those placeholders with the real key.

---

## 2026-08-31 — Dev ports remapped (frontend 6050 / API 6051)

- Vite dev server `5173` → **6050**; API `5080` → **6051**. Updated in `web/vite.config.ts`
  (port + `/api` proxy target), `appsettings.Development.json` (`Urls`), `appsettings.json`
  (`Cors:Origins`), the `Program.cs` CORS fallback, both `launchSettings.json` profiles
  (https stays `7080`), `.claude/launch.json`, and the port references in README / 06 / 07.
- **`.claude/launch.json` api entry fixed**: was `--no-launch-profile`, which skips
  `launchSettings.json` and so never sets `ASPNETCORE_ENVIRONMENT=Development` — the `Urls`
  binding in `appsettings.Development.json` was therefore ignored and the API bound the default
  port instead of the one Vite proxies to. Now `--launch-profile http`.
- No production/runtime behaviour change — dev wiring only. Build clean, 11/11 tests pass.

**Gotchas**
- `npm run dev` here leaves an orphaned `node` child if the wrapper process is killed; the orphan
  hot-reloads `vite.config.ts` and can re-claim the new port. Kill the child, not just the wrapper.
- Pre-existing, surfaced when the `P5_ConcurrencyToken` migration applies to an existing DB:
  EF warns that `PRAGMA foreign_keys = 0` cannot run in a transaction, so an interrupted migration
  needs manual reversion. Harmless on a throwaway dev DB; worth splitting out before any real one.

---

## 2026-08-31 — Handover doc refresh (P5-complete)

- [07-handover.md](07-handover.md) brought fully current: status table (all 6 phases ✅, 11 tests),
  repo map now lists every context / controller / web page, concurrency added as rule #10,
  gotchas 14–17 (IsConcurrencyToken property, `apiUpload` vs `api()`, 409 middleware, NuGet audit),
  new **§14 screen ↔ endpoint ↔ permission map** for quick orientation.
- No code change.

---

## 2026-08-31 — P5 complete (concurrency, user admin, attachments UI, UX polish)

**Backend**
- **Optimistic concurrency**: `AuditableEntity.ConcurrencyToken` (Guid, cross-provider — SQLite
  has no `rowversion`), regenerated in `SaveChangesAsync`, `IsConcurrencyToken` on every auditable
  entity. `DbUpdateConcurrencyException` → clean **409** in the exception middleware.
  Replaces the unused `byte[] RowVersion`. Migration `P5_ConcurrencyToken`.
- **User administration** (`UserService` + `UsersController`, `users.manage` gated): list / create /
  update (name, role, active) / reset password / set extra permissions (Sub-Owner) / set site
  assignments (Supervisor). Guards: at-least-one-active-Owner, no self role-change / self-deactivate.
- 11 tests (added `ConcurrencyTests` — stale write rejected).

**Frontend**
- **Users** admin page (`More → Users`): list, create sheet, edit sheet with role-conditional
  permission checkboxes (Sub-Owner) and site checkboxes (Supervisor) + password reset.
- **Site edit** — Sites list rows are now tappable → edit sheet (`PUT /sites/{id}`), status included.
- **Attachments UI**: `AttachmentPanel` (list + upload via new `apiUpload` FormData helper +
  download via authed blob + delete) wired into Purchase detail and Material Request detail.
- **UX**: `SkeletonList` placeholder replaces the spinner on list/detail loads; `Confirm` dialogs
  now guard Material Request **issue**/**cancel** and Purchase **post** with consequence text.
- More menu tidied (permission-filtered link list) + hint that simple-master editing is API-only for now.
- Bundle ~325 KB / 96 KB gzip.

**This closes the 6-phase plan (P0–P5).** Remaining items are the optional P6 backlog in
[07-handover.md §11](07-handover.md): simple-master admin UI, material-request Scenario-B wiring,
richer report filters, inter-site transfers, PWA/offline, notifications, multi-company.

**Gotchas**
- `IMutableProperty` uses the settable `.IsConcurrencyToken` property, not a `Set…` method.
- `apiUpload` is separate from `api()` — the JSON helper sets `Content-Type: application/json`
  which breaks multipart; never send `FormData` through `api()`.

---

## 2026-08-31 — Handover doc

- Added **[docs/07-handover.md](07-handover.md)**: run instructions, full repo map, the 10 rules,
  the approval-engine extension pattern, cost-flow diagram, a step-by-step "add a feature" recipe,
  frontend conventions, the testing setup, the accumulated gotchas table, a prioritised backlog,
  and the branch/PR workflow. Linked as the first entry in the README docs list.
- Everything through this point is committed and pushed to `origin/main`.

---

## 2026-08-31 — Milestone: P0–P4 complete + P5 in progress

**Status.** The core Construction Business OS is functional end to end:

```
Purchase → Site Inventory → Material Request → Owner Approval → Consumption → Project Cost
Project  → Expense Head/Subhead → Material / Labour / Contractor / Other
Project  → Contractor → Contract Work → Payments → Outstanding
Project  → Customer  → Receipts → Outstanding
```

Every P1–P4 e2e smoke script passes against one fresh DB (cross-phase integration coherent);
10 unit/integration tests green; `dotnet build` + `npm run build` clean.

**Left for a v1.0 hardening pass** (backlog, not blocking use):
attachments UI, optimistic concurrency tokens, site edit + user admin screens, skeleton loaders,
more confirm dialogs, purchase-type material request UI, richer report filters, PWA manifest.

---

## 2026-08-31 — P5 polish (round 2)

**Done**
- **CI**: `.github/workflows/ci.yml` — `dotnet build + test` (Release) and `npm ci + build` on
  push / PR to `main`.
- **Attachments**: `AttachmentService` on `IFileStorage` (type allow-list, 15 MB cap) +
  `AttachmentsController` (list / upload multipart / download / delete). Verified upload + list e2e.
- **Project edit**: ProjectDetail gains an Edit sheet (name, villa no., **customer**, type,
  estimated cost, sale value, status) → `PUT /projects/{id}`. Fills the gap that blocked customer
  receipts on non-demo projects.

**Next (P5 remaining / backlog)**
- Attachments UI (upload control on Purchase + Expense detail).
- `RowVersion` optimistic concurrency (app-generated token, cross-provider).
- Site edit form; Supervisor site-assignment management; user admin screen.
- Skeleton loaders; confirm dialogs on issue / post / cancel actions.
- Material request "Scenario B" (purchase-type request → auto-linked PO) UI wiring.

---

## 2026-08-31 — P5 polish (round 1)

**Done**
- **Tests** (`tests/Swarnakshi.Tests`, 10 passing): `InventoryBalance` weighted-average math +
  no-double-count invariant (pure); SQLite-in-memory integration over the real Application services —
  purchase → weighted avg → request → approve → issue with `MaterialCost + remaining stock value
  == purchase value`; issue blocked before approval; labour cost posts only after approval;
  contractor overpayment blocked without override / negative balance with override;
  customer receipt requires a project customer. `TestHost` wires real DI + seeded masters.
- **Audit log**: `AppDbContext.SaveChangesAsync` now writes `AuditLog` rows for `AuditableEntity`
  creation and every `Status` transition (with remarks), no recursion.
- **NU1903 noise**: `NuGetAuditMode=direct` in `Directory.Build.props` (transitive
  SQLitePCLRaw / Cryptography.Xml warnings from EF Core 10 GA — nothing newer to move to).
- **UX**: reusable `Confirm` dialog; Approval Center approve/reject now confirms and spells out
  the consequence ("inventory / ledgers / project cost update immediately").
- **Docs**: `docs/06-deployment.md` (local, migrations, `dotnet publish`, env vars, static frontend,
  SQLite→SQL Server, backups). README gains Tests + deployment links.

**Next (P5 remaining)**
- Attachments endpoint + UI (invoice / receipt upload) on the existing `IFileStorage`.
- `RowVersion` concurrency tokens (app-generated GUID token works cross-provider).
- Skeleton loaders; confirm dialogs on issue/post/cancel; master-list client caching.
- GitHub Actions CI (`dotnet test` + `npm run build`).
- Project edit form (currently create-only in the UI); assign/change customer.

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
- API dev URL fixed to `http://localhost:6051` via `Urls` in `appsettings.Development.json`
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
