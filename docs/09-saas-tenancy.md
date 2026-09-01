# 09 — SaaS & Multi-Tenancy

Swarnakshi runs as SaaS: one deployment, many builders. **Swarnakshi is now one customer** —
a company with several sites — and any other builder can register and get their own.

---

## The three levels

```
PLATFORM            EnterpriseAdmin — licences and admin passwords. No company data, ever.
   │
COMPANY (tenant)    Acme Builders (code: acme). Own users, masters, numbering, everything.
   │
SITE                Green Valley, Sunrise Villas — one common inventory each
   │
PROJECT             Villa 101, Villa 102 …
```

Everything that existed before now sits under **Company**. Nothing above it did.

---

## Identity: `username@companycode`

A company user signs in as **`owner@swarnakshi`** — username, `@`, company code. It reads like an
email deliberately, because that is familiar, but it is not one: the right-hand side is the tenant.

That single choice buys three things:

- **Usernames need only be unique per company.** Every builder can have an `owner`, a `ravi`, a
  `store`. The unique index is `(CompanyId, Username)`.
- **The company code is the only globally unique string**, which is why it cannot be duplicated
  while company *names* freely can — two builders may legitimately trade under the same name.
- **One login box serves both audiences.** A platform operator has no company, so it signs in with
  a bare username and no `@`. `LoginIdentity.TryParse` splits at the last `@`; no `@` means platform.

Company codes: 2–30 characters, lowercase letters, digits and hyphens, must start and end
alphanumeric, no `@`. Typed in capitals they are accepted and normalised — being strict there would
only punish someone with caps lock on.

---

## Registration

`POST /api/register` — public, because this is how a tenant comes into existence.

```json
{ "companyName": "Acme Builders", "companyCode": "acme",
  "username": "ravi", "password": "…", "confirmPassword": "…",
  "contactEmail": null, "contactMobile": null }
```

It creates the company, its first Owner (`IsCompanyAdmin = true`), and then **provisions the tenant**
— its own units, the 50-category material taxonomy with specification fields, expense heads and
subheads, labour categories, payment methods, project types and default settings.

Every company owns its **own copy** of the master data rather than sharing a global catalogue: a
builder must be able to rename a category or retire a unit without changing anybody else's product.

New companies get a **30-day trial** (`Registration:TrialDays`). `GET /api/register/code-available?code=`
lets the form say "taken" while typing.

---

## Isolation: how it is enforced

Not by remembering to add `WHERE CompanyId = …` in every query. Three mechanisms, none of which a
new feature can forget:

**1. `CompanyId` on `BaseEntity`.** Every tenant row carries it. `Company` and `PlatformUser` derive
from `PlatformEntity` instead, so they are structurally outside tenancy.

**2. A global query filter on every `ITenantOwned` entity**, applied by reflection in
`OnModelCreating` — so a newly added entity is isolated the moment it joins the model:

```csharp
private Guid? CompanyScope => _hasScopeOverride ? _scopeOverride : currentUser?.CompanyId;

private void ApplyTenantFilter<T>(ModelBuilder b) where T : class, ITenantOwned
    => b.Entity<T>().HasQueryFilter(e => e.CompanyId == CompanyScope);
```

`CompanyScope` is a **property, resolved per query** — not a field snapshot taken in the constructor.
A snapshot would freeze whatever the identity happened to be when the context was first resolved,
which, if anything touches the context before authentication completes, is nobody. (This was a real
bug, caught by `MultiTenancyTests`.)

When it is null — anonymous, or a platform operator — every tenant table filters to **nothing**.
That is the fail-safe direction.

**3. An insert stamp in `SaveChangesAsync`.** New tenant rows get `CompanyId` automatically; a write
with no tenant in scope **throws** rather than writing an orphan row that would belong to nobody and
be visible to nobody.

**Composite unique indexes.** Every uniqueness rule is now `(CompanyId, …)` — site codes, project
codes, material codes, `SpecSignature`, party codes, every `TxnNumber`, and `TransactionSequence`.
So each company numbers its own documents from `PUR-2026-00001`; a shared counter would leak how much
business another tenant is doing.

**Crossing the filter deliberately** — the login lookup, the platform console — uses
`BeginTenantScope(companyId)` or `IgnoreQueryFilters()`. Both are rare and explicit.

---

## The EnterpriseAdmin

```
Username: EnterpriseAdmin
Password: SivAyAAn@HMS
```

A `PlatformUser`, not a `User`. It has **no CompanyId**, so every tenant query filter excludes it by
construction — the isolation is structural, not a permission check that could be mis-configured.

It can do exactly two things, plus the housekeeping around them:

| | |
|---|---|
| **Reset a company admin's password** | `POST /api/platform/companies/{id}/reset-password` |
| **Move a licence expiry** | `PUT …/license` (exact date) · `POST …/license/extend` (by days) |
| Suspend / reactivate a company | `PUT …/active` |
| List companies with licence state and admin logins | `GET /api/platform/companies` |
| Change its own password | `POST /api/platform/change-password` |

It **cannot** open any company's sites, projects, stock, money or reports. Try it and every one of
those endpoints answers **403** — verified in both the test suite and the end-to-end script.

The console is its own screen with its own shell: no bottom tab bar, no company navigation, because
a platform operator has no company to navigate.

Defaults are configurable under `Platform:` in `appsettings`. They apply **only at creation** — once
the row exists the seeder never touches it, so a changed password is not quietly reset by a restart.

---

## Two gates, on every request

`[TenantOnly]` sits on every company controller and `[PlatformOnly]` on the console. Between them:

- a platform token is refused by company endpoints (**403**) and vice versa;
- a **suspended** company is refused (**403**);
- an **expired licence** is refused (**402**) — checked per request, not only at sign-in, because an
  access token outlives the moment it was issued and a licence that lapses mid-session has to stop
  working without waiting for it;
- a token issued **before the account's last password reset** is refused (**401**). Clearing the
  refresh token alone would leave a live access token working for the rest of its hour — which is
  exactly the hour a compromised session would use. `User.TokensValidFrom` versus the token's
  `swk_iat` claim closes it.

The UI warns from 14 days out and turns red at 3, so a builder renews on their own schedule rather
than discovering the problem when the app stops.

---

## Upgrading an existing single-tenant database

`PlatformSeeder` creates the founding company (`swarnakshi`, `Platform:DefaultCompanyCode`) and
**adopts every pre-tenancy row into it** — a raw `UPDATE … WHERE CompanyId = '00000000-…'` per
tenant table, because those rows are exactly the ones the new query filters cannot see.

On a fresh database it matches nothing. On an upgraded one it hands the whole existing business to
the founding company instead of stranding it under an empty tenant id.

The seeded owner becomes **`owner@swarnakshi` / `Owner@123`**.

---

## What changed for a developer

| Before | Now |
|---|---|
| `User.Email` was the login | `User.Username` + `Company.Code`; `Email` is optional contact |
| `auth.LoginAsync(new LoginRequest(email, pw))` | `new LoginRequest("owner@swarnakshi", pw)` |
| `MeAsync(userId)` / `LogoutAsync(userId)` | `MeAsync()` / `LogoutAsync()` — identity comes from `ICurrentUser` |
| `MasterDataSeeder.RunAsync(db, hasher, email, pw)` | `MasterDataSeeder.RunAsync(db)` inside a tenant scope |
| Unique index on `Code` | Unique index on `(CompanyId, Code)` |
| Services could ignore tenancy | They still can — the filter and the stamp handle it |

**Writing a service is unchanged.** Query normally; the filter scopes it. Insert normally; the stamp
owns it. The only thing to remember is the one in §"Isolation" note 3: a write needs a tenant in
scope, and outside a request that means `BeginTenantScope`.

---

## Tests

`MultiTenancyTests` (21) covers registration validation, cross-tenant read isolation on both list and
keyed lookups, per-company uniqueness and numbering, the orphan-write guard, licence expiry and
renewal, suspension, and the EnterpriseAdmin's reach and its limits.

`scratchpad/saastest.mjs` drives the same ground through the live HTTP API in 25 checks, including
the 403/402/401 responses that only exist at the controller boundary.
