# 05 — Progress Log

Newest first. Every PR appends an entry: date, area, what changed, what's next, gotchas.

---

## 2026-09-04 — Backend: the layering held, three things inside it did not

The layer graph is clean and was never in doubt — Domain depends on nothing, Application only on
Domain, Infrastructure and Api on what they should. Controllers are pure delegation. So this was
about what sits *inside* the layers.

**CSV rendering lived in a controller.** Thirty-odd lines of escaping and culture handling in
`ReportsController`, which is supposed to choose a representation and nothing else. It is
`Application/Reports/ReportCsv` now: no ASP.NET types, so the escaping rules can be tested
directly rather than through a request. The controller picks between JSON and a file.

That extraction exposed a bug I had introduced earlier the same day. The row cap on reports
announces itself in `ReportTable.Note` — and the CSV export dropped it. The CSV is precisely the
copy someone opens in Excel and reconciles against, so a file holding the first 5,000 rows while
looking complete is the exact failure the cap exists to prevent. The note is written into the file
now, with a test that says why.

**`DashboardController.cs` held two controllers**, one of them `ReportsController`. Now one file
each, named for what is in them.

**`Configurations.cs` was 24 classes covering 32 entities in one file**, against the convention in
CLAUDE.md. Taken literally that convention means 32 files, and that would be worse:
`MasterCodeConfig` and `TxnNumberConfigs` each configure several entities on purpose, because those
entities share one rule, and splitting them copies the rule into places that can disagree. Split by
bounded context instead — tenancy, sites and projects, master data, inventory, transactions,
approvals, employees — the same contexts the Application layer already uses, so a configuration
sits where someone working on that part would look.

254 tests pass, five of them new and about the CSV.

---

## 2026-09-04 — Frontend: one file per feature, and the rules moved back to the server

`ProjectDetail.tsx` was 798 lines — 13% of the whole client — holding **fourteen components across
five unrelated features**: the overview, material, expenses, contractors and customer tabs, each
with its own data fetching, plus the five sheets they open. A change to how a receipt is recorded
sat beside the code for issuing cement, and every edit risked the wrong screen.

It is now a shell of 247 lines — header, figures, alerts, tab strip, edit sheet — and one module per
tab beside it, each owning its own fetching and its own sheets. The largest is 197 lines.

The extraction was done by giving every new file the original's whole import list and then letting
`noUnusedLocals` say what each did not need, rather than by reading and guessing. `Row`, which two
tabs used, became `LabelRow` in the UI kit, since a generic label-and-figure line is not a tab's
business.

**Two rules moved out of the view.** `estimatedCost * completionPercent / 100` — what the estimate
says should have been spent by now — appeared twice in the UI, and the server was already computing
exactly that expression to derive `burnPercent`; it simply never returned it. It is
`ExpectedCostToDate` on the summary now, so the definition lives in one place and the screen reads
it. And `Reports.tsx` had its own `Intl.NumberFormat` instead of the shared `num()`.

Verified against Villa 104: the server returns 2,640,000 for a ₹44L estimate at 60%, which is what
the UI used to compute, and all five tabs render.

249 tests pass.

---

## 2026-09-04 — Audit: measured the thing, then fixed what the measurement showed

Latency first looked like a flat 15 ms everywhere. It was the measuring tool: Node's fetch opens a
fresh connection per call, and `/health` — no auth, no database — measured 15 ms too. With
keep-alive the real floor is 0.3 ms and nothing exceeded 7 ms on the seeded book.

Ten villas prove nothing about scale, so the tests ran again against a restored clone with the two
growing tables multiplied to 38k and 36k rows — 128x. Statement counts stayed flat, so there is no
N+1 anywhere. What did show up was **ProjectExpenses being table-scanned 649 times in one short
run**: it is the table every cost figure sums, and nothing indexed the way those queries ask.

Two indexes, both taken from SQL Server's own missing-index DMV rather than from guesswork:

    ProjectExpenses       (CompanyId, Status, ProjectId) INCLUDE (ExpenseType, Amount, Date)
    InventoryTransactions (CompanyId, SiteId, Type, Date)

At 128x volume: project summary 70 ms to 6.4 ms, company summary 94 to 13, profitability 35 to 10,
projects list 31 to 9. ProjectId is in the key rather than the INCLUDE because within one tenant
every row shares CompanyId, so the selectivity has to come from somewhere else.

**The reports had no limit at all** — not one `Take` in the service. The consumption register
answered with 1.16 MB at 36k rows, and a few years of trading is tens of megabytes built in memory
on the server and parsed in memory in the browser. Row-level reports now cap at 5,000 rows and say
so in the table, because a report that silently returns the first N and looks complete is worse
than one that is slow.

**Security.** Sign-in was anonymous, unthrottled and about to face the internet: ten attempts a
minute per address now, partitioned by the caller's real IP, which only works because the
forwarded-headers middleware runs first. Verified both directions — brute force stops after exactly
ten, and 80 ordinary signed-in requests pass untouched. Added nosniff, DENY framing and a referrer
policy; no CSP, because the right one differs between the two deployment shapes and a wrong one
fails silently.

**The frontend had no error boundary**, so any render error blanked the app. On a phone, on a site,
that reads as the app being gone and the day's entries with it. There is one now, outside the
router, saying plainly that saved work is safe.

Two of my own defects surfaced on the way. `Backup-Database.ps1` used WITH COMPRESSION, which
Express refuses outright — the production backup would have failed on every run. And the test
databases were accumulating, eighteen of them, because an age-based sweep never catches same-day
leftovers; the sweep now reads the process id out of the database name and drops the ones whose run
is over.

249 tests pass.

---

## 2026-09-04 — Two artefacts: a UI for Cloudflare Pages, an API for IIS

The single-service shape stays and still works. Alongside it, `Publish.ps1` now emits the UI and
the API as separate artefacts, because they are hosted apart — the UI on Cloudflare’s edge, the API
on IIS behind a tunnel. `docs/06b-deployment-split.md` is that runbook, step by step.

**The client could not have worked split.** Every call was built as a relative `/api`, which on
Cloudflare asks the CDN for an endpoint it has never heard of. There is now one `apiUrl()` through
which all four fetch sites go, and a build-time `VITE_API_BASE_URL`. Empty — the default — keeps
the relative behaviour for development and for the API serving its own `wwwroot`.

The UI is built twice from the same source. `appwwwroot` gets the relative build, so
`http://localhost:6061` on the server is a complete signed-in-able site for a smoke test before
anything is uploaded; `frontend` gets the absolute build for Cloudflare. Different bundle hashes
are the proof the address really is baked in. `_redirects` and `_headers` ship with it: without the
first, refreshing a deep link 404s; without the second, a browser keeps an old `index.html` asking
for assets the new deploy has replaced.

**Two bugs found by running the scripts rather than reading them.**

`Publish.ps1` died on `npm notice`. Windows PowerShell wraps any stderr line from a native command
in a NativeCommandError, and under ` = 'Stop'` that ends the script — so a
build failed on a notice while the command itself had succeeded. Native commands now run with the
preference relaxed and are judged by their exit code, which is the only thing that reports failure.

And an intermittent test failure that was not intermittent at all: `CreateOwnAsync` sliced its
database name with `[..60]` when the name is about 59 characters — the width of the process id is
what varies. Every isolated test failed, or none did, depending on the pid. Five consecutive clean
runs since.

249 tests pass.

---

## 2026-09-04 — SQL Server only, and published through a Cloudflare tunnel

**SQLite is gone.** It survived as the test suite’s in-memory database and as a provider branch in
the app; both are removed. `AddPersistence` builds a SqlServer context and nothing else, the
`DateTimeOffset`-as-ticks converters that only SQLite needed are deleted, the design-time factory no
longer has a fallback, and the package references are dropped from three projects.

The suite pays for that. `dotnet test` now needs SQL Server Express: it creates one
`SwarnakshiTest_<pid>_<time>` database, builds the schema once, and gives each of the 210 test hosts
a **tenant** in it rather than a database of its own — which is the isolation the product already
relies on, now exercised a couple of hundred times a run. Forty tests failed on the first attempt,
every one of them a fixture assumption rather than a product bug: they register companies and count
them, adopt rows left by the pre-tenancy upgrade, or sign in as `owner@swarnakshi`. Those are about
the database, not about a tenant in it, so they call `CreateIsolatedAsync()` and get one to
themselves; the rest build their login from `host.CompanyCode`. 45 seconds became 2m10s. Worth it:
what the suite proves now is that the rules hold on the engine the product is deployed on.

**Published through Cloudflare Tunnel.** The service binds `http://localhost:6061` — loopback, not
`0.0.0.0`, because `cloudflared` runs on the same machine and connects outward, so there is no
inbound rule to open and nothing on the network can reach the port. Two hostnames point at the one
process: `cops.sivayaantechnologies.com` for people and `copsapi.sivayaantechnologies.com` for
integrations. The UI calls `/api` relative to whatever host served it, so `cops.` is same-origin and
CORS never applies; `Cors:Origins` carries the UI hostname for the other case.

The app now honours `X-Forwarded-Proto`/`-Host`/`-For`. Without that it believes it is serving
`http://localhost`, because the TLS ended at Cloudflare and what reaches Kestrel is plain HTTP from
the loopback — so anything derived from the request would name the wrong scheme and host.

249 tests pass.

---

## 2026-09-04 — Material is filed under its own category, not Miscellaneous

A delivery note names a material, never a work stage, so every direct-to-villa purchase fell
through to the Miscellaneous head. The totals were right and the split was useless: a villa's
cost-by-head showed cement sitting beside sundry contractor money.

The material already carries a classification, so use it. `ProjectCostWriter.WriteMaterialCostAsync`
now takes the material and, when the caller names no head, files the cost under the head named for
that material's category — cement under Civil & Structure, a pipe under Plumbing. A villa's
breakdown reads as the trade split a builder thinks in.

Heads are found by name and created only when missing, so the seeded stages that already share a
name with a category — Plumbing, Electrical, Painting — are reused rather than duplicated. An
explicit head still wins: a material request that names RCC keeps RCC.

Applies to all three writers — direct delivery, issue against a request, and return to store, so a
return nets the category back off instead of stranding a credit in Miscellaneous.

Verified against the live book: a two-line purchase for Villa 203 landed ₹7,200 under Plumbing and
₹6,000 under Painting, and all ten reconciliation identities still hold.

249 tests pass.

---

## 2026-09-04 — A ten-villa book that reconciles, and what driving it turned up

Built a full book of work through the API — 2 sites, 10 villas at 100/60/10 percent and one still
on paper, 17 purchases, 20 issues, 21 work orders, contractor and customer payments, day labour,
and site-level overhead — then checked that the screens agree with one another.

**The reconciliation was never broken; the line that reported it was.** The old script compared
inventory value against the purchase register's *Sub Total* column, which is before tax, and the
gap it showed was exactly the 18% GST. It now asserts ten identities against the reports the
product actually serves, and all ten hold to the rupee:

    material bought = in stock + consumed          purchase register = company summary
    stock value = company summary                  consumption = material in project cost
    project cost = its four buckets                sum of villa costs = total project cost
    site villa cost + overhead = total cost        capital employed = its components
    stock by site = stock by material              sale - received = outstanding

Then the same thing was entered by hand through the UI — a direct-to-villa purchase, approved
through the queue — and all ten still held, which is the point of doing it twice.

**Three things came out of it.**

The UAT suite was dead: it passed a SQLite connection string to an app that now defaults to SQL
Server, so all 24 scenarios failed before the browser opened. It now creates and drops its own
`SwarnakshiUat_<stamp>` database on the local instance, which also means the acceptance layer
finally exercises the provider production uses.

The burn chip lied by a factor of ten. "110% of budget spent at 10% built" on a villa that had
spent 11% of its budget — the figure is spend against the spend *expected by this stage*, not
against the whole budget. It now reads "Spending 110% of what 10% built should have cost".

Material bought straight to a villa is filed under **Miscellaneous**. The total is right and the
ledger is right — both moves appear, stock nets to zero, the villa is charged — but the by-head
breakdown puts cement next to sundry contractor money, because `ProjectCostWriter` falls back to
the Miscellaneous head when a purchase line carries no head, and the purchase form never asks for
one. The API already takes `ExpenseHeadId` per line. Left alone deliberately: which head
unallocated material belongs to, or whether the form should require a stage, is a decision about
the chart of accounts rather than a bug to quietly patch.

Not fixable from here: Playwright's Chromium will not start on this machine — the Visual C++
redistributable is missing, so the headed run cannot be demonstrated until it is installed.

245 tests pass.

---

## 2026-09-04 — SQL Server Express, and a deployment that has actually been rehearsed

The app ran on SQLite because nothing had been deployed yet. It now runs on **SQL Server Express**,
database **SCOPS**, and there is a deployment plan whose every step has been executed rather than
described.

**The conversion.** The ten SQLite migrations were replaced by one SQL Server `InitialCreate` —
nothing was deployed, so there was no history to preserve. `DateTimeOffset` is a real
`datetimeoffset(7)` now instead of the UTC ticks SQLite needs. Three things broke on the way and
are worth remembering:

- `InvariantGlobalization` was `true`. SqlClient refuses to open a connection without ICU, with a
  message that names globalization and not the database. It must stay `false`.
- `EnableRetryOnFailure` looks like an obvious win and is a trap here: EF forbids
  `BeginTransactionAsync` under a retrying execution strategy, and the six places that post
  inventory and financial side effects all use one. Retries are off, with a comment saying what
  would have to change first.
- The content root followed the process's working directory, so a service starting in
  `C:WindowsSystem32` would have found neither `appsettings.Production.json` nor `wwwroot`.
  It is pinned to the binary folder.

**One process.** The published API serves the built React app from its own `wwwroot`, so the UI and
the API share an origin: no reverse proxy, no CORS in production, one service to install and watch.
A request under `/api` that matches no controller returns 404 JSON rather than falling through to
the SPA shell and answering an API client with a page of HTML.

**Secrets left the repository.** `appsettings.json` carries no connection string and no signing key,
and the app refuses to start outside Development without them rather than falling back to something
well-known. The developer's copy is in user-secrets; the server's is in a git-ignored
`appsettings.Production.json` that the deploy script writes and locks to SYSTEM and Administrators.

**The scripts** in `deploy/` do the whole job: create the database and its least-privilege login,
build a versioned package, back up, deploy, verify `/health`, and roll back on failure. Migrations
run as an explicit `--migrate` step that exits non-zero, so a bad schema change fails the deployment
while the service is still stopped instead of taking the site down under load.

**Rehearsed, not assumed.** SCOPS was created, migrated and seeded; the demo book was rebuilt
through the API — 10 villas, 62 approvals, 81 inventory transactions, 114 project expense rows — so
every transactional posting path has now run on SQL Server. The published package was then started
standalone and checked: `/health`, the SPA shell, a deep link, a 404 under `/api`, and a real login
returning real burn percentages.

245 tests pass. `docs/06-deployment.md` is the runbook.

---

## 2026-09-03 — A General category, so nothing has to be filed in the wrong trade

Nine categories are the nine trades a site is organised into. A generator spare, a wheelbarrow, a
roll of packing tape belong to none of them — and with nowhere honest to put them, they get filed
under whichever trade looks nearest and are effectively lost.

**General** is now the tenth category, deliberately last: General Material, Tool, Machinery Spare,
Fuel & Lubricant, Packing Material, Cleaning Material, Stationery, Other. "General Material" moved
out of Site & Safety into it — re-parented in place, same row Id, so every material, balance and
transaction pointing at it still resolves.

The move rides the existing flatten map, which now also carries `Site & Safety/General Material` →
`General/General Material` for a tenant that was already flattened once. Startup runs it per tenant.

245 tests pass.

---

## 2026-09-03 — Two dropdowns to pick a material, search kept as the alternative

The picker led with a search box. Typing is fast for someone who knows the catalogue and useless
for someone who does not — a storekeeper looking at a delivery note wants to be shown the options,
not asked to guess a word.

`MaterialPicker` now leads with **two dropdowns: material category, then material name**. Nine
categories is a list read in a glance, and choosing one cuts the second list to what that trade
buys — Civil & Structure is 14 of the seeded 40 rather than all of them. On a phone both open as
the native wheel, so nothing has to be typed. "Search by name instead" swaps them for the old box
for whoever prefers it, and "Pick from the lists instead" comes back.

Picking still collapses to a settled row — name, category / type, unit, and a Change link — so the
row stops asking a question once it has been answered. Purchases, material requests and Add stock
all get this, the direct-to-villa purchase included.

245 tests pass.

---

## 2026-09-03 — A villa's material list says which trade each line belongs to

The Material tab listed a name and a date. "Vitrified Tiles 600x600" tells you what arrived;
it does not tell you the villa has spent ₹1.25L on flooring.

`InventoryTxnDto` now carries `CategoryName` and `MaterialTypeName` alongside the material name —
a name on its own is ambiguous in a list, and after the taxonomy flattening a type like "Elbow" or
"Fittings" says very little until you know it is plumbing. Every row shows the material name with a
category chip, and the second line gives the type, the date, the movement and the transaction
number.

Above the list, the total is now broken down by category. That is the question an owner actually
asks of a material list — where the money went — and a chronological ledger never answers it.
Villa 101 reads: Civil & Structure ₹4.92L, Flooring & Stone ₹1.25L, Electrical ₹31,294, Plumbing
₹23,789, Painting ₹23,541.

245 tests pass.

---

## 2026-09-03 — Nine categories, and a material picker you type into

Choosing a material meant scrolling a `<select>` of every material in the company, under a
taxonomy of fifty categories. Both halves are fixed.

**The taxonomy is nine categories** — Civil & Structure, Plumbing, Electrical, Flooring & Stone,
Doors & Windows, Painting, Hardware & Fasteners, Roofing & Ceiling, Site & Safety. Everything that
used to be a category ("CPVC Plumbing", "Plumbing Valves", "Distribution Boards") became a material
type inside one, which is the level people name things at anyway. Type names are self-describing
because in a search box they appear with no parent to lean on: `CPVC Elbow` not `Elbow`, `OPC
Cement` not `OPC`.

The move is `MaterialTaxonomy.Flatten` plus `MaterialMasterSeeder.FlattenTaxonomyAsync`.
**Subcategory rows are re-parented and renamed in place, never recreated** — every Material,
InventoryBalance, InventoryTransaction, PurchaseItem and MaterialRequestItem points at that row, so
nothing breaks. The old fifty are deactivated, not deleted. Collapsing eleven plumbing parents into
one put `Elbow` and `Fittings` on a collision course; the rename map splits them, and
`Every_material_type_name_is_unique_inside_its_category` is the test that says so.

**`DbInitializer` now runs the taxonomy seeder for every company on startup**, not just the
founding one. Other tenants are provisioned once at registration and never seeded again, so a change
to the tree would otherwise reach exactly one company. The seeder is idempotent and returns
immediately when a tenant is already current.

**`MaterialPicker`** replaces every material dropdown — purchases, material requests, add stock.
Type and it searches the server (name, brand, category, type); the nine category chips narrow the
same search rather than replacing it. Nothing is fetched until there is something to narrow by, so
the screen never loads four hundred rows to throw them away. Picking one collapses it to a settled
row with a Change button rather than leaving a text box still asking a question.

Verified against the live `sivayaan2` data: 9 active categories, 50 retired, materials and
inventory all still resolve under their new parents. 245 tests pass.

---

## 2026-09-03 — Type the supplier on a purchase, do not set one up first

Recording a delivery no longer starts with a trip to the supplier master. The supplier field on
the purchase form is now a text box with an autocomplete list of existing names. Type an existing
one and it is matched case-insensitively; type anything else and a bare supplier — just the name
and an auto `SUP-` code — is created as the purchase is saved. GSTIN, bank details and the rest
can be filled in later on the new **More → Suppliers** screen.

- `SavePurchaseRequest.SupplierId` is now `Guid?`, joined by `string? SupplierName`; the validator
  requires one of them. `PurchaseService.ResolveSupplierAsync` does id → existing-by-name → create
  and pulls in `ICodeGenerator`.
- Creating the supplier this way happens inside `purchase.create`, so a Supervisor can name a new
  supplier without `masters.manage`. A supplier is a name; blocking the purchase over it is worse.
- The frontend resolves the typed name against its loaded list, sending `supplierId` on a match and
  `supplierName` otherwise, with a "New supplier — …will be added" hint while typing.
- New `Suppliers.tsx` reuses `PartyMaster`; route `/suppliers`, linked from More — it was
  previously reachable only by URL.

7 test files updated for the record's new shape, 4 new in `TypeableSupplierTests`. 244 pass.

---

## 2026-09-03 — A Supervisor no longer sees the company dashboard or the reports

A site Supervisor runs a site — raise requests, record purchases, keep projects moving. The
company overview (its cash position, receivables, contractor payable) and the reports are the
office's view, not theirs.

**New permission `dashboard.view`.** `DashboardController` now requires it. `Permissions.ForRole`:
Owner (via `All`), Sub-Owner and Accountant have it; Supervisor does not. Supervisor also loses
`reports.view` — `ReportsController` already gated on it, so that endpoint 403s for them too. A
Supervisor's token now carries exactly `inventory.view`, `material_request.create`,
`purchase.create`, `projects.manage`.

**Frontend follows the token.** `App.tsx` renders the index route as the dashboard only when
`dashboard.view` is held, otherwise `<Navigate to="/projects">`; the `/reports` routes redirect the
same way. `AppShell` drops the "Home" tab when there is no dashboard — a Supervisor's bar is four
tabs (Projects · Inventory · Approvals · More), grid switched to `grid-cols-4`. `More` hides its
"Review" section without `reports.view`.

Verified live: a Supervisor gets 403 from `/api/dashboard` and `/api/reports/*`, 200 from
`/api/projects`; in the UI they land on Projects with a four-tab bar and no Reports anywhere.

The role→permission `[Theory]` in `AuthAndUserTests` gained cases for both keys across all four
roles. 240 tests pass.

---

## 2026-09-03 — Sign in with a mobile number

The login box now takes a bare 10-digit mobile number as well as `username@companycode`. On a
phone, for a supervisor, the number is what they know; the `@company` half is friction.

**Parsing.** `LoginIdentity.TryParse` decides in order: a value with `@` is a company login; a value
made only of phone characters (`^[d+(][ds-()]*$`) is a mobile; anything else is a bare
username, which is the platform operator. `NormaliseMobile` strips a `+91`, spaces, brackets or a
trunk `0` down to the canonical 10 digits — that is what `User.Mobile` stores and matches on.

**The lookup crosses the tenant filter, on purpose.** A number does not name a company, so
`AuthService.MobileLoginAsync` is the one login path that runs `IgnoreQueryFilters()`. It takes the
first two matches: none → the same 401 as a bad password (a stranger must not learn a number is
registered); one → sign in; two → a 409 telling the person to use `username@companycode`, because
the same number in two companies is genuinely ambiguous and guessing would be worse.

**Uniqueness within a company** is enforced in `UserService`, not by a unique index — a filtered
unique index is provider-specific and SQL Server treats NULLs as equal in a plain one, both of
which break the SQLite-agnostic rule. The index on `Mobile` is there for lookup speed only.

**Set at registration.** `contactMobile`, if it is a real 10-digit number, becomes the founding
owner's `Mobile`, so a new company can sign in by phone from the first minute. The Users screen has
a Mobile field on the create and edit sheets for everyone else.

Migration `UserMobileLogin` adds the nullable column and its index — existing users have
`Mobile = null` until someone fills it in. 12 new tests in `MobileLoginTests` (234 total).

---

## 2026-09-01 — Demo captions are paced to be spoken

The content engine's review found the transcript unusable as an audio script, and it was right. A
demo run held every caption for a flat 1.5s, so a 26-word beat got the same window as a 6-word one.
Narrating it aloud at 160 wpm needs 9.8s against a 2.7s window — nearly four times over, with every
later cue drifting further behind the picture. Measured across one journey the audio would have
finished about 25 seconds after a 13-second video.

Fixed at the source rather than worked around downstream: the hold is now proportional to the text
(`SWARNAKSHI_UAT_SPEECH_WPM`, 160 by default, plus padding and a floor). The same journey now runs
41s with every cue carrying 1.1-1.9s of headroom over its speech estimate, so one speech clip per cue
placed at `startMs` fits by construction.

`estimatedSpeechMs` is published per cue so a consumer can detect overrun — a slower voice or a
longer translation — rather than discovering it in the finished video.

Also documented: **record one viewport at a time.** A journey runs desktop then mobile and the window
resizes to 390x844 between them, which would land mid-capture. `&DisplayName~Desktop` on the filter
selects one, verified against `--list-tests`. The content-engine brief said to filter by journey
only, which would have produced exactly that broken recording.

---

## 2026-09-01 — The content-engine brief lives in docs

[10-content-engine](10-content-engine.md) is the brief handed to the Sivayaan content engine for
building demo videos from the narration transcripts.

It sits in this repository rather than that one because the contract it describes is ours — the JSON
shape, the journey list, the run commands, the environment variables. Kept there it would go stale
silently; kept here it is the file to change in the same commit as whatever broke it.

---

## 2026-09-01 — Narration transcripts alongside each run

The narration was unreachable outside the browser: captions lived only on screen, and xUnit surfaces
the step list only when a test fails, so a green run left nothing to caption a recording with.

Every run now writes `artifacts/uat/narration/<Journey>-<Viewport>.json` — the journey's identity and
business purpose, plus each narration beat as a cue with start and end times.

`endMs` is when the next line replaced it rather than a fixed duration, because a caption stays up
while its step runs; an eight-second step gets an eight-second cue. Times are measured from the
title card, since a recording starts whenever the camera does and an absolute clock would align with
nothing. Written for failures too, where the transcript ends at the step that broke.

Best effort throughout: a transcript that cannot be written is a missing convenience, never a failed
scenario.

---

## 2026-09-01 — UAT runs headed

The suite ran headless, so watching it was impossible and the answer to "did anything happen?" was a
log line. The browser is now visible by default — this suite is the walkthrough of the product as
much as its test, and a run nobody can see is one nobody can film or trust.

It turns itself off on CI, where there is no display and a headed Chromium fails to launch rather
than falling back. The `uat` job also sets `SWARNAKSHI_UAT_HEADED=false` explicitly, so the reason is
visible where it matters rather than resting on an environment variable being noticed.

Headed is only visibility. Pacing and captions still belong to demo mode
(`SWARNAKSHI_UAT_RUN_MODE=demo`), which was already there and is unchanged, so an ordinary run is
still as fast as the browser will go — 24 cases in about 100 seconds headed, against 75 headless.

---

## 2026-09-01 — Project progress: stage counts and a completion percentage

The Projects screen showed a flat list and one status chip. It now answers the question the office
actually asks — how much of the book of work has not started, is under way, and is finished — and
carries a completion percentage the site enters and updates.

**No second status field.** `ProjectStatus` already encodes the stage (Planned → Active/OnHold →
Completed, with Cancelled as the deactivated case). A parallel "progress" enum would have let the two
contradict each other — a project Completed but 40% done — so the counts are grouped from the status
that already exists, and the percentage is the detail within a stage rather than a rival to it.

- **Not started** = Planned; **In progress** = Active + OnHold; **Completed** = Completed.
- **Cancelled is reported apart from the buckets.** A cancelled villa is not "not started", and
  counting it there would overstate the work still to come.
- **On hold is counted as under way** — work that started and stopped — and also reported on its own.
- The average completion covers only what is under way, so a yard full of planned villas does not
  drag it to zero.

`CompletionPercent` (0-100) is entered by the site rather than derived from cost: money spent is not
progress built, and on a villa the two diverge constantly — the material for a whole slab is bought
on day one. Two rules keep it honest with the stage: completing a project settles it at 100, and a
project still Planned is refused if it reports progress (rejected rather than silently corrected —
the fix is a decision only the user can make).

The migration backfills already-completed projects to 100. A flat default of 0 would have reported
every finished villa as untouched and dragged the average down with it.

UI: stage counts and an average bar above the list, a progress bar on each project under way, and
the percentage editable in both the create and edit sheets, with the bar tracking the field live.
Selecting Completed or Planned sets and locks the number rather than letting the server correct it
afterwards.

`ProjectProgressTests` — 10 tests over the buckets, cancelled exclusion, the average, the per-site
filter, an empty book of work, and the two consistency rules. 203 fast tests and 24 UAT cases pass.

---

## 2026-09-01 — An upgraded database could not be logged into, or migrated at all

Running the app against a real `swarnakshi.db` — one that predates multi-tenancy — found two bugs
that no test could have seen, because every test starts from an empty database.

**Nobody could sign in.** The migration adds `Users.Username` with an empty default and never
backfills it, and `PlatformSeeder` creates its founding owner only when there are NO users — which
on an upgraded database is never. The company ended up with users who existed, were active, and
matched no login: the sign-in resolves a username within the company, and `""` matches nobody.

**Worse, a real install could not migrate at all.** The same migration builds a UNIQUE index on
`(CompanyId, Username)` over that empty column. With one user it applies; with two it fails and the
upgrade aborts at startup. Any company with an owner and a supervisor was blocked. This is the same
mistake as the `SpecSignature` index in P6 — populate the column *before* building the unique index
over it — and it was made again three weeks later.

Fixed in two places:

- The migration writes `CAST(Id AS varchar(64))` into empty usernames before creating the index.
  The row id rather than anything email-derived, because extracting a local part needs
  `instr`/`CHARINDEX` and those differ per provider.
- `PlatformSeeder` recognises both the empty default and that id placeholder as "not a login" and
  derives a real one from the email's local part — before multi-tenancy the email *was* the login,
  so `owner@swarnakshi.local` becomes `owner` and the person signs in as `owner@swarnakshi`. Falls
  back to the name, dedupes with a numeric suffix, and validates against `LoginIdentity`. It runs on
  every startup, so a database already left in the broken state heals on restart rather than needing
  a manual UPDATE. It also promotes the adopted owner to company admin, which the migration
  defaulted to false.

`UpgradeFromSingleTenantTests` covers the path: 7 tests over the empty default, the id placeholder,
collisions, a user with no email, idempotency, and the invariant that every adopted user ends up
with a login that is valid and unique.

Verified on the real database: it healed on restart (`Username` empty → `owner`, `IsCompanyAdmin`
set) and signing in as `owner@swarnakshi` reaches a dashboard with the business's real figures.

**Gotchas**
- Tests all start from an empty database. The upgrade path is a separate path and needs its own
  tests — these are the first.

---

## 2026-09-01 — CI runs the acceptance suite, and a dead server now says so

**CI was green while UAT was 0/24.** `ci.yml` runs `dotnet test` at the root, which the `Uat` gate
excludes the browser suite from — so multi-tenant sign-in could break every acceptance journey
without a single red check. That gap was created by the gating and is closed here: UAT is its own
job, with `npm ci` for the client the suite starts, `playwright install --with-deps chromium` (the
on-demand install does not bring a bare runner's system libraries), and failure screenshots uploaded
as an artifact.

The exact CI command was validated locally in Release — but not under CI's actual conditions, and
the first real run caught it: `ApiServer` hardcoded `-c Debug` while passing `--no-build`. CI builds
the solution in Release only, so it launched a binary that was never produced and every scenario
failed with "the API exited with code 1". It passed locally purely because stale Debug output
happened to be lying around. The configuration now follows the one the assembly was built in.

Reproduced properly this time by deleting `src/Swarnakshi.Api/bin/Debug` first, which is what a
runner actually looks like: **24/24** in Release with no Debug output present, and 24/24 on the
normal Debug command.

**A server dying mid-run now fails loudly.** One Release run came back 14 red — 13 of them
`ERR_CONNECTION_REFUSED` because the Vite client had exited minutes earlier. The real event was in
`web.log`; the test output showed a dozen unrelated-looking timeouts. `ApiServer`/`WebDevServer`
expose `IsAlive`, and each scenario checks both first, so the run now stops at the first case after
the death and names the server and its log.

The flake itself is not diagnosed — Vite logged nothing before exiting, and the run passes on
repeat. This makes it legible rather than fixing it. If CI shows it, the honest next step is to serve
a built client (`vite preview`) instead of a dev server, which has no watcher to fall over.

**Handover corrections** (§9 and §17 of `07-handover.md`): the testing section still claimed 11 tests
across 4 classes — it is 186 across 15 — and gave the UAT command without `-p:Uat=true`, which
reports no tests and exits 0. The gating landed before that section was rewritten, so it was already
wrong when written. Gotchas 27 and 28 added for the tab list and the `username@companycode` login.

---

## 2026-09-01 — UAT follows multi-tenancy and the reordered menu

Syncing to main left the acceptance suite **0 / 24**. Two product changes it had not been told about,
both legitimate:

**Sign-in changed shape, not just wording.** A login is no longer an email but
`username@companycode`, resolved against a company rather than a global user table. Every case died
on the login form. The seeded owner is now `owner@swarnakshi`, and its display name is the *company*
name, because that is what `PlatformSeeder` writes into `User.Name` for a founding admin. The
credentials-rejected message moved from "Invalid email or password." to "…username or password.",
and the user list no longer prints a login at all — since multi-tenancy the company half is the same
for everyone on screen.

**The bottom tabs were reordered by daily use.** Sites and Stock were demoted to More; Movement and
Inventory took their place. Twelve cases were waiting on a "Stock" tab that no longer exists. The
`/stock` hub page itself is unchanged — the same four cards — so only the route to it moved, and it
is now reached through More as "Stock & purchases".

That the suite noticed both, and named the business step it died on rather than a selector, is the
whole point of it. Back to **24 / 24**.

**Gotchas**
- `TabBarLabels` in `WorkflowContext` encodes a product decision — what a site engineer reaches in
  one tap. When the menu is reordered, that list is the thing to update; everything not in it is
  reached through More automatically.

---

## 2026-09-01 — Handover rewritten around the six use cases

§6c was a table of which tests covered what — useful for auditing, useless for picking the project up.
It is now a **walkthrough**: run each of the six journeys in the app, in the order that makes sense
(fill the store, empty some of it, then the special case), with the exact figures to expect at each
step, what to try in order to break it, and which files to open when changing it.

Each use case now carries: **Walk it** (click path + expected numbers) · **Try to break it** ·
**Code** (entity → service → screen) · **Tests**.

Also corrected several stale facts the doc had accumulated — it still claimed 11 tests and listed
neither the Employees, Movement, Register nor PlatformConsole screens, nor the later migrations.

**Every figure in the walkthrough was re-verified against a fresh database** by replaying the
documented steps through the live API (`scratchpad/walkthrough-check.mjs`, 20 checks): store
100@₹400, blend to 200@₹425, issue 50 → villa ₹21,250 with ₹63,750 left, direct-to-villa leaving the
store untouched at 150@₹425 while the villa gains ₹45,000, both ledger rows present, wrong-site
delivery refused, receipts ₹25L against ₹80L leaving ₹55L, and remarks round-tripping.

Fixed one thing the doc got wrong: it showed a `curl` for turning on `purchase.needs_approval`,
which has no endpoint. It is a SQL update until the settings screen exists.

---

## 2026-09-01 — Named use cases pinned by tests, and remarks on every daily entry

**Six journeys, tested as described** (`UseCaseWalkthroughTests`, 12 tests) — store→villa movement,
direct-to-villa purchase, adding to the store, the approval gate on both purchases and movements,
customer payments, and simple entry carrying remarks. Table in [07-handover §6c](07-handover.md).

The approval tests pin the gate from three sides: issuing before submit, issuing while still pending,
and a rejected request — none of which may move a single bag.

**Remarks now surface in the UI** where they already existed in the API: a delivery note on a purchase
("Lorry AP09 XX 1234"), a reason on a material request ("First-floor slab"), and a note on a customer
receipt. A number with no note is a number nobody can explain three months later.

---

## 2026-09-01 — Direct-to-villa purchases, and a menu ordered by daily use

**Purchase straight to a villa.** `PurchaseItem.DeliverToProjectId` — per line, so one invoice can put
the cement on Villa 101 and the steel into the store. On post it receives into site stock and issues
to the project in the same transaction, then books the project material cost.

The issue uses **the line's own landed rate**, not the site's weighted average. That charges the villa
what was actually paid, and it is also the only rate that leaves the store untouched: receiving *q* at
*r* and issuing *q* at *r* restores quantity, value and average exactly. Worked example and the full
reasoning are in [07-handover §6b](07-handover.md).

Guard: the target project must be on the purchase's site — inventory is site-level. Checked at entry
as well as at post, so the mistake surfaces while someone is still typing.

**Menu reordered by how often each screen is actually opened**, as requested:
`Home · Movement · Inventory · Projects · More`. New **Movement** hub — request material, approve,
issue, record a purchase, project spend — with awaiting-approval and ready-to-issue counts. Sites,
material master, stock/purchases, contractors, customers, employees and reports moved under More;
they are set-up-or-review work, not daily work.

**Verified** — 6 new tests (174 total, green) plus the live-API walkthrough: store 200@₹400 before and
after, villa charged ₹45,000, ledger showing both movements, and ₹1,25,000 = ₹45,000 + ₹80,000.

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

## 2026-09-02 — Simplified for the people who actually use it

The app was built by someone who knows the data model, for someone who does not. Five changes,
all in that direction.

**Codes are the app's business now.** Sites, projects, materials, employees, contractors and
customers mint their own (`SITE-0001`, `PRJ-0007`) via a new `ICodeGenerator`. Every save request's
`Code` became optional; supplying one still works, so an office that already numbers its sites keeps
doing so. The subtle half is edits: an update that omits the code keeps the existing one rather than
renumbering the record. `AutoCodeTests` pins both halves down.

One real bug fell out of writing those tests — `MaterialService` added the new `Material` to the
change tracker before minting its code, and minting commits the sequence row, so SQLite tried to
flush a half-built row with a null Code. The code is now allocated before the entity is tracked.

**The material master asks five questions.** Name, category, subcategory, brand, description. Unit
is behind a "set a unit of measure" link and falls back to the company default. Commercial
information, inventory controls and the specification matrix are gone from the screen — purchases
carry the real rate and tax, inventory carries the real stock, so none of it was ever this form's
job.

**Inventory got an "Add stock" button.** Material, quantity, cost per unit. Putting 100 bags into
the system used to mean raising a purchase with a supplier and an invoice; that path is still there
for a real invoice, one tap away under Purchases.

**Navigation is five tabs: Home · Projects · Inventory · Approvals · More.** Movement and Stock
were hubs of hubs and are gone, their routes redirected. `More` is two short lists — set up, review.
The shell widens to `max-w-5xl` on desktop, so the office gets room for tables while the phone
layout is untouched.

**A villa has three kinds of entry, and the tabs say so.** Overview · Material · Expenses ·
Contractors · Customer. Material offers "Take from store" and "Bought for this villa", both of which
open pre-filled with the villa's site and destination. Labour folded into Expenses and contractor
payments into Contractors — on site those were never separate things.

`PageHeader` now renders a back arrow on every screen that is not one of the five tab roots, so
there is always a way out.

**Next:** a colour theme the business picks rather than inherits.

---

## 2026-09-02 — Blueprint, and icons that were drawn rather than typed

The palette is now **Blueprint**: drafting blue on cool paper by day, cyanotype at night. Chosen
from five directions mocked up on real screens.

The old gold had a defect worth recording, because it is easy to reintroduce. `--brand` and
`--warn` were the same value, so a low-stock warning was the same colour as every button on the
screen — the one thing that should shout looked exactly like the things that shouldn't. In Blueprint
the accent is a hue no status colour uses: approved green, pending amber and cancelled red each read
as themselves against it. Light mode also gained real contrast, which matters because half the
users are outdoors.

**Icons are SVG now, hand-drawn, in `components/icons.tsx`.** The app was navigating on `⌂ ▤ ▦ ✓ ☰`
and emoji, which render differently on every device and at every weight. The set is ~4KB with no new
dependency, drawn on a 24-unit grid at constant stroke weight to match the drafting theme — and half
of it is construction-specific (hard hat, cement sack, tower crane, tipper) which no icon package
ships. A logomark went on Login and the registration success screen: a villa's gable inside a
surveyor's crosshair.

Three icons were redrawn after looking at them at actual tab-bar size rather than in isolation. A
rubber stamp is the truer metaphor for approving something, but at 21px it reads as a trophy, so
Approvals is a checked clipboard. Same for the hard hat (read as a bell until the brim widened) and
the cement sack (read as a clipboard until the neck narrowed). **Judge an icon at the size it ships
at.**

Two fixes fell out: `PageHeader` grew a back arrow on `/projects/` because the trailing slash missed
the tab-root set — routes are normalised now. And Sign out was a full-width solid red slab; it is
outlined, with the loud red kept for things that actually cancel a transaction.

---

## 2026-09-02 — A real book of work, and what it exposed

Built a full demo tenant through the public API — `scratchpad/seed-demo.mjs`, no direct database
writes — so every number came out of the same validation, approval and posting code a real user
drives. 2 sites, 10 villas (3 handed over, 3 at 50%, 3 at 10%, 1 on paper), 11 purchases, 12 store
issues, 21 work orders, 27 contractor payments, 12 labour entries, 13 customer receipts, 62 items
through the approval queue.

**Fixed: purchases were posting without anybody agreeing to them.** `purchase.needs_approval` seeded
as `"false"` and the code fell back to `false` when the row was missing, so a supervisor's purchase
reached both site stock and the supplier ledger unseen. Both now default to **true** — an unapproved
purchase that reached stock is worse than one that waited. That change broke 21 tests, which is the
correct amount of noise for a change this wide; `ApprovalHelpers.SubmitAndApproveAsync` gives the
tests that only want stock on the shelf a one-liner, and the tests that are *about* approval still
drive `IApprovalService` directly.

**Found, not yet fixed — material cost is dated when you type it, not when it happened.**
`MaterialRequestService` passes `clock.Today` to both the stock ledger and the cost writer, and
`IssueRequest` carries no date at all. In the seeded book all 54 material cost rows — ₹35.35L, the
largest single cost category — carry today's date, against requests dated across five months. The
purchase path does this correctly (`purchase.Date`), as does the return path (`req.Date`); only the
issue path does not. Consequence: "Expenses this month" is meaningless, every period cost report is
wrong, and month-end cut-off does not work — material issued on 31 March and entered on 2 April
lands in April.

**Found — profit is overstated by crediting unearned revenue.** `ProjectFinancialSummary.Margin` is
`ContractSaleValue - TotalCost` with no reference to `CompletionPercent`. A villa at 50% shows the
whole ₹58L sale against the ₹8.62L spent so far and reports ₹49.38L of "profit". Across the book the
app claims ₹2.58Cr against a percentage-of-completion figure of ₹1.53Cr — **overstated by ₹1.05Cr.**

**Found — committed contractor money is invisible at villa level.** ₹24.81L is contracted but unpaid.
The company summary shows it; no villa's `TotalCost` does, so every in-progress villa understates
its cost to complete.

**Found — payroll reaches no cost figure.** Four employees, ₹1.19L/month, roughly ₹11.9L over the
period. `EmployeePayment.ProjectId` is optional and nothing prompts for it, and there is no site
overhead bucket at all — every cost must attach to a villa.

Not a defect, though it looked like one: the purchase register's ₹43.64L is the pre-tax subtotal
while stock is valued at the ₹51.49L landed total. They reconcile exactly. `consumed ₹35.35L +
on hand ₹16.14L = ₹51.49L purchased` holds to the rupee.

---

## 2026-09-03 — The six findings, fixed

Everything the seeded book of work exposed, in the order it was worth doing.

**Earned revenue.** `ProjectFinancialSummary` gained `EarnedRevenue`, `EarnedMargin` and
`CompletionPercent`. Revenue is recognised in proportion to how much of the villa is actually
built, so a villa at 50% carries half its sale value instead of all of it. Villa 104 reported
₹49.38L of profit on ₹8.62L of spend; it now reports ₹20.38L. Across the seeded book that is
₹1.05Cr of imaginary profit removed. `Margin` is still on the record — some callers want the
contracted figure — but the screens read `EarnedMargin`.

**Burn rate.** `BurnPercent` is spend over what the estimate says should have gone by this stage.
It is on the villa screen, on the villa list row, and in its own report, with a chip over 100% and a
red one over 110%. Villa 201 — 109% spent at 10% built — was invisible before and is now the first
thing on its screen. Estimate-minus-actual is still there but demoted: on an unfinished villa it
shows a large positive that reads as money saved.

**Issue dates.** `IssueRequest` gained a `Date`, threaded through to both the stock ledger and the
cost writer, defaulting to the request's own date rather than `clock.Today`. Verified on a fresh
tenant: 54 material cost rows, 0 dated today, each matching the day the material left the store. The
old tenant's rows still carry the wrong date — **existing installations need a one-off backfill from
each row's source transaction.**

**Dues on handover.** `DuesOnHandover` on the summary, a red banner on the villa, a flag column in
the report. Villa 103 is complete with ₹14.75L owed and now says so.

**Committed contractor cost.** `CommittedContractorCost` and `CommittedTotalCost` on the summary —
the unpaid balance of open work orders. Not in `TotalCost`, because nothing has left the bank, but
shown next to it: "spent ₹4.27L · + ₹4.06L committed". A `Contractor Commitment` report lists it by
work order.

**Site-level costs.** New `SiteExpense` entity, deliberately a separate table rather than a nullable
`ProjectId` on `ProjectExpense`. The invariant that a project's cost is exactly the sum of its
ProjectExpense rows is what stops material being double counted; loosening it to allow orphan rows
would risk that for the sake of a different kind of cost. Site overhead appears in Site Summary and
Company Summary and stays out of every villa's cost — dumping the watchman on Villa 104 would make
one villa look expensive and its neighbours cheap.

**Five new reports**, all with the colour treatment that makes a flag read as a flag: Villa Profit &
Loss, Budget vs Progress, Site Summary, Contractor Commitment, Supplier Outstanding. The Reports hub
is regrouped into Profit / Money / Inventory.

**Still open.** No screen for the approval settings — `purchase.needs_approval` can only be changed
in the database. Employee payments can be charged to a project but nothing prompts for it, and they
cannot yet be charged to a site; the `SiteExpense` bucket they would write into now exists.

222 tests pass, 12 of them new in `ProfitReportingTests`.

---
