# 08 — UAT (user acceptance testing)

The UAT suite drives the real product in a real browser: it starts the API and the Vite client,
signs in as the seeded owner, and performs the business journeys a builder actually performs. It is
the acceptance layer above `tests/Swarnakshi.Tests`, which asserts the same rules arithmetically
against the services.

Modelled on the UAT suites in `TransTruck_Web` (`tests/TransTrack.UatTests` +
`tools/TransTrack.Automation`) and `HMS_WEB`.

---

## Layout

| Project | What it is |
|---|---|
| `tools/Swarnakshi.Automation` | Playwright automation library: server management, browser session, and the **scenarios themselves** as `IWorkflow` objects |
| `tests/Swarnakshi.UatTests` | xUnit acceptance suite — one class per module, each running one workflow in both viewports |

The scenarios live in the automation library rather than in the test project on purpose: the same
objects can be replayed headed with captions (`SWARNAKSHI_UAT_RUN_MODE=demo`) to produce a
walkthrough, so what is demonstrated and what is signed off are the same journey by construction.

---

## Running it

```bash
dotnet test tests/Swarnakshi.UatTests -p:Uat=true
```

**The `-p:Uat=true` is required.** Without it the project reports no tests and exits quietly, which
is the price of keeping it out of the solution-wide `dotnet test`: this suite starts servers and
drives a browser for minutes, and nobody should pay that on a bare `dotnet test`. The project stays
in the solution, so `dotnet build` still compiles it and it cannot rot unnoticed — only the test run
is gated. If a UAT run reports "no tests", you left the switch off.

Do not pipe it into `tail`/`head` while it runs — those buffer until the process exits, so a run in
flight looks silent and you lose the per-scenario PASS/FAIL lines as they happen. Redirect instead
(`> uat.log 2>&1`) and tail the file.

It is **not** part of a bare `dotnet test` at the repo root — gated by `IsTestProject`, not by habit.
Run it deliberately, and in CI as its own step.

One scenario, one viewport:

```bash
dotnet test tests/Swarnakshi.UatTests -p:Uat=true --filter "FullyQualifiedName~MaterialCatalogueUatTests"
```

### Ports

The suite runs on **6070 (client) / 6071 (API)** — deliberately not the 6050/6051 a developer uses.
A UAT run must never attach to, or write into, a dev server someone has open. Both are started with
strict ports, so a clash fails loudly instead of silently hopping.

The client's `/api` proxy is pointed at the run's own API through `SWARNAKSHI_API_URL`, which
`web/vite.config.ts` reads (defaulting to 6051 for normal development).

### Database

Each run creates a throwaway SQLite file under `artifacts/uat/` and deletes it afterwards. The suite
signs in, creates materials and posts stock, so pointing it at a developer's `swarnakshi.db` would
both leave data behind and make its assertions depend on whatever was already there.

---

## Configuration

Every value has a working default; a bare `dotnet test` needs no configuration.

| Variable | Default | Purpose |
|---|---|---|
| `SWARNAKSHI_UAT_BASE_URL` | `http://localhost:6070` | Where the client is served |
| `SWARNAKSHI_UAT_API_BASE_URL` | `http://localhost:6071` | Where the API is served |
| `SWARNAKSHI_UAT_RUN_MODE` | `test` | `test` (headless) or `demo` (headed, captioned) |
| `SWARNAKSHI_UAT_VIEWPORT` | `desktop` | Default viewport; the suite overrides it per case |
| `SWARNAKSHI_UAT_MANAGE_SERVERS` | `true` | `false` to attach to servers you started yourself |
| `SWARNAKSHI_UAT_MOBILE_DEVICE` | `iPhone 15 Pro` | Playwright device descriptor for the mobile viewport |
| `SWARNAKSHI_UAT_DESKTOP_SIZE` | `1440x900` | Must clear the `lg` breakpoint (1024px) where the master tables appear |

---

## Scenarios

Every scenario runs in **both viewports**. That is not ceremony: the master screens render a desktop
table (`hidden lg:block`) and mobile cards (`lg:hidden`) from the same component, so a scenario that
passes on one proves nothing about the other.

| Key | Module | What it accepts |
|---|---|---|
| `SignIn` | Security | Sign out, a wrong password refused, the right one admitted |
| `Dashboard` | Overview | Projects, sites, inventory value and receivable on one screen |
| `UserAccess` | Security | User administration and the Approval Centre are owner-reachable |
| `MaterialCatalogue` | Material Master | Search by name **and by specification value**; filters clear |
| `AddMaterial` | Material Master | Subcategory-driven specification fields; the material joins the catalogue |
| `MaterialLifecycle` | Material Master | Deactivate with confirmation, find under Inactive, reactivate |
| `ContractorMaster` | Party Master | Add, find, deactivate with the history-preserving confirmation |
| `CustomerMaster` | Party Master | Add, find, and see what already references the record |
| `PurchaseToConsumption` | Procurement | Buy material into a **site**, and see it in that site's stock |
| `MaterialRequestApproval` | Procurement | Request → owner approval → issue, as three separate acts |
| `SiteInventory` | Inventory | Stock scoped per site, searchable |
| `Reports` | Reporting | The standing reports render and export |

---

## Why the assertions look the way they do

**`Visible()` before `First()`, everywhere.** Swarnakshi ships both layouts in the DOM and hides one
with a breakpoint, so every material name and action button exists twice on every master screen. A
plain `.First` binds to whichever copy is first in the DOM — on desktop that is the hidden mobile
card — and then waits out its timeout for a visibility that will never come.

**The tab list is a product decision, and the suite tracks it.** `TabBarLabels` names what a site
engineer reaches in one tap. When the menu was reordered by daily use — Sites and Stock demoted to
More, Movement and Inventory promoted — twelve cases failed on a tab that no longer existed. Update
that list; anything not in it is reached through More automatically.

**Sign-in is `username@companycode`.** Since multi-tenancy a login is resolved against a company, not
a global user table, and the seeded owner's display name is the *company* name (`PlatformSeeder`
writes it into `User.Name`). `DemoData` holds all three so a seed change is one edit, not a hunt.

**Navigation by clicking, never by URL.** A navigation that only works from the address bar is not
one a site engineer has. `NavigateAsync` uses the bottom tab bar, and routes everything not on it
through "More" — which, unlike most apps of this shape, is the same in both viewports.

**Unique codes per run.** The Material Master refuses an exact duplicate of name + brand +
specification, and the party masters refuse a duplicate code. That refusal is a feature, so the
scenarios that create records suffix them with a timestamp rather than trip over it on a re-run.

**`Field(caption, tag)` rather than `GetByLabel`.** The `Field` component renders
`<label><span>caption</span>{control}</label>` — the control is *inside* the label, so the implicit
label's accessible name is the whole text content, which for a `<select>` includes every option.
`GetByLabel("Category *", exact)` can therefore never match a select. Matching the caption span
exactly and reaching in for the control is what actually identifies the field.

**`ConfirmAsync` for dialogs.** Confirmation dialogs reuse the verb of the row action that opened
them, so an unscoped button lookup finds the row button now behind the overlay and fails as "element
not stable". Confirmation clicks are scoped to `role=dialog`.

**Signing in returns to the current route**, not to the dashboard — so the SignIn scenario navigates
Home before asserting the greeting, exactly as a user would.

---

## When a scenario fails

The failure message names the business step that broke, not just a locator:

```
UAT scenario 'Adding An Exact Material' (AddMaterial) failed on Mobile.
Purpose: Define one exact purchasable material — brand and specification included …

Failed after 4 step(s):
   1. [Material Master] Adding An Exact Material
   2. Adding a material starts from the Material Master screen.
   3. The identity comes first: the company's own code, the material, and who makes it.
   4. Choosing the subcategory decided which specifications apply …

Reason: Timeout 20000ms exceeded.
Screenshot: artifacts/uat/AddMaterial-FAILED-Mobile-20260831-181200.png
```

A screenshot of the failing screen is written to `artifacts/uat/` for every failure.

---

## Current status

**All 24 cases pass** — every scenario in both viewports. The harness itself (server start, throwaway
database, login, navigation, screenshots) is stable.

---

## What UAT found in the product

These were real defects in Swarnakshi, not test problems. They are recorded because each one was
invisible to the backend suite by construction — the services were correct; the failure was in how
the browser reached them.

| Defect | Effect |
|---|---|
| `[FromQuery] PageQuery page` on 13 endpoints | The parameter name collided with the `page` query key, so ASP.NET switched to prefixed binding and **`?q=…` was ignored**. Every search box in the product returned unfiltered results. Renamed to `paging`, with the reason recorded on `PageQuery` itself. |
| `useAsync` had no request-ordering guard | Lists re-query on every keystroke, so several requests are in flight at once; whichever the server answered LAST won. A slow early response overwrote the results the user was looking at. |
| A mutation's refresh used stale filters | A handler closes over `reload` at click time; if a filter changed while its POST was in flight, the refresh re-queried with the *old* filters and — being newest — won the ordering guard. The list contradicted its own controls: Status "All", rows Active-only. `useAsync` now reads `fn` through a ref. |
| No supplier was ever seeded | With no supplier UI either, a purchase could not be recorded at all on a fresh install. |

---

## Traps this suite has already fallen into

Kept because each cost a run and none is obvious from the DOM:

**Filter the row CONTAINER by visibility, not just the control inside it.** Both layouts ship in the
DOM at once, and `TableWrap` is *also* a `div.rounded-2xl` carrying every row's text — so on mobile a
container match binds to the hidden desktop table and then waits out its timeout for a button that
can never appear. `Row()` filters the container; everything row-scoped goes through it.

**Never take a row action with a bare button lookup.** Every row offers the same verbs, so an
unscoped locator takes the first on screen. This deactivated a seeded material instead of the one the
scenario created, and the run failed several steps later against a record nothing had touched.

**Search is debounced.** Asserting straight after typing can pass against the *unfiltered* list — in
which a freshly created record is also visible. `SearchAsync` waits for the list to settle.

**Assert the effect, not the network.** After a lifecycle action, wait for the row to actually read
`Inactive` (`ExpectRowStatusAsync`). A bare network wait let a run continue while the list still
showed the old state.

**`ASPNETCORE_URLS` does not override `Urls` in appsettings.** `CreateBuilder` layers app config over
host config, so the UAT API silently bound the developer's own port. It is passed as `--urls` instead.

**Assertion timeouts are separate from page timeouts** — `Assertions.SetDefaultExpectTimeout` is set
explicitly; the page timeout does not cover `Expect`.
