# 06 — Build & Deployment

Swarnakshi runs as **one Windows service** talking to **one SQL Server Express database**. The
service is the API and it also serves the built React UI out of its own `wwwroot`, so a browser on
the site's hostname makes only same-origin calls. Cloudflare Tunnel publishes it: `cloudflared` runs
beside the service and connects outward, so the machine needs no inbound firewall rule, no public IP
and no certificate of its own.

| | |
|---|---|
| Database | SQL Server Express, instance `.\SQLEXPRESS`, database **SCOPS** |
| Database login | `SivayaanHMS` (SQL authentication) |
| Service | `Swarnakshi`, automatic start, runs as LocalSystem |
| Listens on | `http://localhost:6061` — loopback only; the tunnel reaches it from this machine |
| Public UI | `https://cops.sivayaantechnologies.com` |
| Public API | `https://copsapi.sivayaantechnologies.com` |
| Installed at | `C:\Swarnakshi\app`, data under `C:\Swarnakshi\data`, backups in `C:\Swarnakshi\backups` |
| Health check | `http://localhost:6061/health` → `{"status":"ok"}` |

Both public hostnames point at the same local service. `cops.` is what people open; `copsapi.` is
the same API under a name integrations can use. The UI calls `/api` relative to whatever host it was
loaded from, so on `cops.` nothing is cross-origin and CORS never comes into it. `Cors:Origins`
exists for the other case — a browser on some *other* site calling `copsapi.`.

> **Hosting the UI and the API apart?** If the UI goes to Cloudflare Pages and the API to IIS —
> two hostnames, two artefacts — follow [06b — Deploying the UI and the API separately](06b-deployment-split.md)
> instead. This guide is the single-service shape.

---

## 1. The first deployment

Do these once, in order, on the server. Everything after this is section 2, which is three commands.

### 1.1 Prerequisites

| Needed | Check | If missing |
|---|---|---|
| SQL Server Express | `Get-Service 'MSSQL$SQLEXPRESS'` | Install SQL Server Express |
| `sqlcmd` | `Get-Command sqlcmd` | Ships with the SQL Server Client SDK |
| ASP.NET Core 10 runtime | `dotnet --list-runtimes` | Install the .NET 10 Hosting/Runtime, **or** publish with `-SelfContained` and skip this |
| Node 20+ and npm | `node -v` | Only needed on the machine that runs `Publish.ps1`, not on the server |

`Publish.ps1 -SelfContained` bundles the .NET runtime into the package. The package is larger, and
the server then needs nothing installed but Windows and SQL Express. For a single-server deployment
that is usually the right trade.

**The Visual C++ x64 redistributable is not one of these.** It is a prerequisite of the UAT suite
only — Playwright's Chromium is linked against it, and without it a UAT run fails at browser launch
with `spawn UNKNOWN` (see `docs/08-uat.md`). The server does not need it: the published app was run
to completion on a machine carrying only the x86 redistributable, migrating and seeding SQL Server
without complaint. Nothing in ASP.NET Core or Microsoft.Data.SqlClient depends on it, and the UAT
suite has no business running on a production box in the first place.

### 1.2 Create the database

The application needs a database and a SQL login that can reach it. Create them however you
normally do — by hand in SSMS, or with the script:

```bash
sqlcmd -S .\SQLEXPRESS -E -C -i deploy\sql\01-create-database.sql -v AppPassword="<password>"
```

If you create it by hand, these are the settings that matter and the rights the login needs.

**On the database**

| Setting | Why |
|---|---|
| `READ_COMMITTED_SNAPSHOT ON` | Posting an approval holds a transaction. Without this, every dashboard query queues behind it. |
| `AUTO_CLOSE OFF` | Express defaults it on, which costs a slow first request after every idle period. |
| `AUTO_SHRINK OFF` | Shrinking fragments the indexes it just rebuilt. |
| Recovery `SIMPLE` | Right unless you schedule log backups. See section 4. |

**On the login**

| Grant | Why |
|---|---|
| `db_datareader`, `db_datawriter` | Ordinary reads and writes |
| `GRANT EXECUTE` | Stored procedure execution |
| `CREATE TABLE`, `ALTER ON SCHEMA::dbo`, `REFERENCES ON SCHEMA::dbo` | EF Core migrations create and alter tables and indexes, and write to `__EFMigrationsHistory` |

`db_owner` covers all of that and is fine if that is simpler for you. The script grants the
narrower set deliberately: enough to run and migrate the app, not enough to drop the database.

Nothing else needs creating — the application builds all 42 tables itself on the first deployment.
If you would rather build them yourself, see the next section.

### 1.2a Applying the schema by hand

Optional. `Deploy.ps1` applies the schema itself, and if you have already applied it, it finds the
work done and reports that the schema is up to date. Do it by hand where only a DBA may change the
schema — and then the application login never needs `CREATE TABLE` or `ALTER` at all, so you can
drop those three grants from the script above.

The scripts live in **`deploy\sql\`**, and travel in every package `Publish.ps1` builds:

| Script | What it does | Run as |
|---|---|---|
| `01-create-database.sql` | Creates the database, the login and the user, and grants their rights | sysadmin, once per server |
| `03-schema.sql` | Creates all 43 tables, 184 indexes and 64 foreign keys | sysadmin or db_owner, on the database from step 1 |
| `02-rotate-password.sql` | Changes the application login's password | sysadmin, whenever you rotate it |

```bash
sqlcmd -S .\SQLEXPRESS -E -C -b -d SCOPS -i deploy\sql\03-schema.sql
```

`-b` matters: without it `sqlcmd` returns success even when a batch failed, and a half-applied
schema looks like a clean run.

Two things about `03-schema.sql` worth knowing before you run it.

**It is idempotent.** Every migration is wrapped in a check against `__EFMigrationsHistory`, so
running it twice does nothing the second time, and running it against a partly-migrated database
applies only what is missing. That is also what makes it the right script for later releases: apply
the same file again and it adds only the new migrations.

**It creates the schema, not the data.** The platform operator, the founding company, the expense
heads, units and the material taxonomy are seeded by application code the first time the service
starts — there is no SQL for them. So the order is: create the database, apply the schema, then let
`Deploy.ps1` start the service, which seeds the master data and finds the schema already in place.

It is a generated file. After adding a migration, regenerate it rather than editing it:

```bash
powershell -File deploy\scripts\New-SchemaScript.ps1
```

`Publish.ps1` runs that itself, so a package can never ship binaries expecting one schema beside a
script that builds another.

### 1.3 Build the package

On a build machine, from a clean checkout of the commit you intend to ship. This needs the .NET SDK
and Node, which is why it is not usually done on the server:

```bash
powershell -File deploy\scripts\Publish.ps1
```

It refuses to build from a dirty working tree (`-AllowDirty` for a throwaway test build), runs
`dotnet test` as the gate, builds the UI with `npm ci` into the API's `wwwroot`, publishes the API,
and writes `deploy\out\` containing `app\`, `sql\`, `scripts\`, the settings template, and a
`build.json` naming the commit.

`-SelfContained` bundles the .NET runtime into the package. Larger, but then the server needs
nothing installed but Windows.

Copy that folder to the server, for example to `C:\Swarnakshi\packages\<version>`.

### 1.4 Write the settings file

**This is the only configuration on the server, and the only file you ever edit.** Copy the
template and fill it in:

```bash
New-Item -ItemType Directory -Force -Path C:\Swarnakshi\app
```

```bash
Copy-Item .\appsettings.Production.template.json C:\Swarnakshi\app\appsettings.Production.json
```

```bash
notepad C:\Swarnakshi\app\appsettings.Production.json
```

Three things must change from the template:

| Key | What to put |
|---|---|
| `ConnectionStrings:Default` | Your server, database, login and password |
| `Jwt:Key` | A random string of at least 32 characters. Generate one, then leave it alone — it signs every token, so changing it later signs everyone out. |
| `Urls` | `http://localhost:6061`. Loopback, not `0.0.0.0`: the tunnel runs on this machine, so nothing else should be able to reach the port. |
| `Cors:Origins` | `["https://cops.sivayaantechnologies.com"]` |

To generate a signing key:

```bash
powershell -c "$b=New-Object byte[] 48;(New-Object Security.Cryptography.RNGCryptoServiceProvider).GetBytes($b);[Convert]::ToBase64String($b)"
```

The connection string is an ordinary SQL Server one, so the machine name, instance, database, login
and password are all yours to change here — now or at any point later:

```
Server=.\SQLEXPRESS;Database=SCOPS;User ID=SivayaanHMS;Password=...;TrustServerCertificate=True
Server=SQLBOX01\SQLEXPRESS;Database=SCOPS;User ID=app;Password=...;TrustServerCertificate=True
Server=10.0.0.5,1433;Database=SCOPS;Trusted_Connection=True;TrustServerCertificate=True
```

With `Trusted_Connection=True` the service authenticates as its own account. It runs as LocalSystem,
so the login to grant in SQL Server is then `DOMAIN\MACHINENAME$`.

If you would rather not hand-edit, `Deploy.ps1 -InitSettings -ConnectionString '<yours>'` writes the
file for you and generates the signing key.

### 1.5 Deploy

From inside the copied package, in an **elevated** PowerShell:

```bash
powershell -File .\scripts\Deploy.ps1
```

No passwords on the command line — it reads them from the settings file. It checks that the database
is actually reachable with those credentials before touching anything, installs the `Swarnakshi`
service, applies the schema, starts it, and waits for `/health`.

### 1.6 Publish it through Cloudflare Tunnel

Nothing inbound is opened. `cloudflared` makes an outbound connection to Cloudflare and traffic
comes back down it, so there is no firewall rule to add and no port exposed to the network.

Install it, and sign in to the Cloudflare account that holds the zone:

```bash
winget install --id Cloudflare.cloudflared
```

```bash
cloudflared tunnel login
```

Create the tunnel once. It writes a credentials file under `%USERPROFILE%\.cloudflared\`:

```bash
cloudflared tunnel create swarnakshi
```

Point both hostnames at it. This creates the DNS records in Cloudflare for you:

```bash
cloudflared tunnel route dns swarnakshi cops.sivayaantechnologies.com
```

```bash
cloudflared tunnel route dns swarnakshi copsapi.sivayaantechnologies.com
```

Write `%USERPROFILE%\.cloudflared\config.yml`. Both hostnames go to the same local service — the
one process serves the UI and the API:

```yaml
tunnel: swarnakshi
credentials-file: C:\Users\<you>\.cloudflared\<tunnel-id>.json

ingress:
  - hostname: cops.sivayaantechnologies.com
    service: http://localhost:6061
  - hostname: copsapi.sivayaantechnologies.com
    service: http://localhost:6061
  # Cloudflare requires a catch-all; anything not matched above is refused.
  - service: http_status:404
```

Check it in the foreground first, then install it as a service so it starts at boot:

```bash
cloudflared tunnel run swarnakshi
```

```bash
cloudflared service install
```

Two services now start automatically: `Swarnakshi` (the app) and `cloudflared` (the tunnel). The app
does not depend on the tunnel — it serves `localhost:6061` regardless — so a tunnel restart never
touches the database or the running work.

The app trusts `X-Forwarded-Proto` and `X-Forwarded-Host`, which is how it knows a request that
arrived at Kestrel as plain HTTP from localhost was really `https://cops.…` at the edge.

### 1.7 First login, and the one thing to do immediately

Browse to `https://cops.sivayaantechnologies.com/`.

| Account | Login | Purpose |
|---|---|---|
| Founding tenant owner | `owner@swarnakshi` | The company's first Owner |
| Platform operator | `EnterpriseAdmin` | Licence renewal and password resets only. Never sees company data. |

Both are seeded with the application's built-in default passwords unless you set a `PlatformAdmin`
section in the settings file. **Change them at first login.** The seeder sets a password only when it
creates the row, so a changed password is never quietly reset by a restart or a redeployment.

---

## 2. Every deployment after the first

```bash
powershell -File deploy\scripts\Publish.ps1
```

Copy `deploy\out` to the server, then, elevated, from inside it:

```bash
powershell -File .\scripts\Deploy.ps1
```

No arguments, no database script, no settings to re-enter. `Deploy.ps1` reads the settings file
already on the server, preserves it, and treats the run as an upgrade. In order it:

1. reads and validates the settings file, and **proves the database is reachable** with those
   credentials — so a wrong password fails on line one, not half way through;
2. **backs up the database** and verifies the backup, labelled with the version being replaced;
3. **copies the current release to `C:\Swarnakshi\previous`**, so a rollback has a target;
4. stops the service;
5. swaps in the new binaries, keeping `appsettings.Production.json` and everything under
   `C:\Swarnakshi\data`. `wwwroot` is emptied rather than merged — a stale hashed asset left behind
   would be served to a browser that has just been handed a new `index.html`;
6. **applies migrations as an explicit step** (`Swarnakshi.Api.exe --migrate`), which exits non-zero
   on failure. This is why the schema change is not left to happen on first request: a bad migration
   fails the deployment while the service is still stopped, instead of taking the site down under
   traffic;
7. starts the service and polls `/health` until it answers;
8. and if anything from step 4 onward fails, puts the previous release back and starts it.

Expected downtime is the length of steps 4–7, normally under a minute.

If the deploying account cannot run `BACKUP DATABASE`, step 2 stops the deployment rather than
skipping quietly. Grant it `db_backupoperator`, take a backup yourself, or pass `-SkipBackup` to
proceed without one deliberately.

### Making a schema change

```bash
dotnet ef migrations add <Name> --project src/Swarnakshi.Infrastructure --startup-project src/Swarnakshi.Api --output-dir Persistence/Migrations
```

The migrations in `src/Swarnakshi.Infrastructure/Persistence/Migrations` target **SQL Server**. Keep
them additive — add columns and tables, backfill, and drop only in a later release once no deployed
binary reads the old shape. That is what makes a binaries-only rollback safe.

The design-time factory reads `Database__Provider` and `ConnectionStrings__Default` from the
environment, so point it at a scratch database rather than production when scaffolding.

---

## 3. Rolling back

**A release that deployed but is behaving badly** — swap the binaries back:

```bash
powershell -File C:\Swarnakshi\packages\<version>\scripts\Rollback.ps1
```

This restores `C:\Swarnakshi\previous` and leaves the database alone. Because migrations are
additive, the previous binaries ignore the columns they do not know about, and no data is lost.

**A release whose migration is the problem** — restore the backup that `Deploy.ps1` took just before
it. This loses everything entered since, so it is the second choice, not the first:

```bash
Stop-Service Swarnakshi
powershell -File .\scripts\Restore-Database.ps1 -BackupFile C:\Swarnakshi\backups\SCOPS-<stamp>-pre-<version>.bak -Confirm
powershell -File .\scripts\Rollback.ps1
```

`Restore-Database.ps1` verifies the file, takes its own safety backup of the current state first, and
refuses to run while the service is up.

---

## 4. Backups

`Deploy.ps1` backs up before every upgrade, but a backup that only exists on deployment days is not a
backup. Schedule a nightly one:

```bash
$a = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -File C:\Swarnakshi\app\..\scripts\Backup-Database.ps1'
$t = New-ScheduledTaskTrigger -Daily -At 1:30am
Register-ScheduledTask -TaskName 'Swarnakshi nightly backup' -Action $a -Trigger $t -User SYSTEM -RunLevel Highest
```

Backups go to `C:\Swarnakshi\backups`, are compressed, verified with `RESTORE VERIFYONLY`, and pruned
after 30 days — except the newest, which is never pruned even if it is older than that.

SCOPS is in **SIMPLE** recovery, so these full backups are the whole story and the log cannot grow
without bound. Point-in-time recovery would mean switching to FULL recovery *and* scheduling log
backups; do both or neither.

**Copy the backups off this machine.** A backup on the same disk as the database survives a bad
deployment and nothing else.

---

## 5. Configuration

**One file, and you edit it directly:**

```
C:\Swarnakshi\app\appsettings.Production.json
```

`Deploy.ps1` reads it, validates it, and preserves it across every upgrade — it is never overwritten,
so an edit made today survives the deployment made next month. It is **git-ignored**; the committed
`deploy/appsettings.Production.template.json` is the annotated copy to start from.

`appsettings.json` also ships in the package but holds no secrets, and the settings file above wins
over it for every key.

| Key | Notes |
|---|---|
| `ConnectionStrings:Default` | Server, database, login, password. Change the machine name, instance, or password here. |
| `Database:Provider` | `SqlServer` |
| `Database:CommandTimeoutSeconds` | 60 |
| `Jwt:Key` | ≥32 characters. The app refuses to start outside Development without it. **Set once, then leave it.** |
| `Urls` | Address and port, e.g. `http://localhost:6061` |
| `PlatformAdmin:Username` / `:Password` | EnterpriseAdmin seed credentials, used only when that row is first created. Omit the section to keep the built-in default. |
| `Cors:Origins` | Empty. The UI is same-origin; add an entry only if another site must call this API. |
| `Seed:Demo` | `false`. Demo data is Development-only and is ignored in Production regardless. |
| `Storage:LocalRoot` | Attachment directory, `C:\Swarnakshi\data\uploads` |

**After any change to this file:**

```bash
Restart-Service Swarnakshi
```

Any key can also be overridden by an environment variable, using `__` for `:` —
`ConnectionStrings__Default`, `Jwt__Key`. The file is simpler; the variables are there for the
occasions when you need to override without editing.

### Changing the database password, or moving to another SQL Server

Rotate the password in SQL Server:

```bash
sqlcmd -S .\SQLEXPRESS -E -C -i deploy\sql\02-rotate-password.sql -v NewPassword="<new>"
```

Then edit `ConnectionStrings:Default` in the settings file to match — the password, the `Server=`,
or both if the database has moved — and restart:

```bash
Restart-Service Swarnakshi
```

**Do not touch `Jwt:Key` while you are in there.** Every issued access and refresh token is signed
with it, so replacing it signs every user out; changing a database password does not have to.

---

## 6. Local development

Development runs against the same SQL Express instance, so what you test is what you ship. The
connection string lives in user-secrets, outside the repository:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Server=.\SQLEXPRESS;Database=SCOPS;User ID=SivayaanHMS;Password=<password>;TrustServerCertificate=True" --project src/Swarnakshi.Api
dotnet run --project src/Swarnakshi.Api    # http://localhost:6051, API docs at /scalar/v1
cd web && npm install && npm run dev       # http://localhost:6050, proxies /api to 6051
```

In development Vite serves the UI and proxies `/api`; there is no `wwwroot`, and the SPA-fallback
code is skipped. Only the published build serves the two from one process.

To work against a throwaway copy rather than SCOPS itself:

```bash
sqlcmd -S .\SQLEXPRESS -E -C -i deploy\sql\01-create-database.sql -v AppPassword="<password>"   # edit :setvar DbName
```

Set `Seed:Demo` to `true` in `appsettings.Development.json` to have that database filled with demo
data on startup. It is Development-only and cannot fire in Production.

### The test suite runs on SQL Server too

There is no second database provider any more. `dotnet test` needs SQL Server Express on
`.\SQLEXPRESS` (override with `SWARNAKSHI_TEST_SQL_SERVER`), where it creates one
`SwarnakshiTest_<pid>_<time>` database, builds the schema in it once, and drops it at the end.

Each of the two hundred-odd test hosts then takes a **tenant** in that shared database rather than a
database of its own — which is the isolation the product itself relies on, so running them together
exercises it a couple of hundred times a run. Tests that are about the database rather than about a
tenant in it — registering companies and counting them, adopting rows left by the pre-tenancy
upgrade, signing in across two companies — call `TestHost.CreateIsolatedAsync()` and get a database
to themselves.

It costs time: the suite went from about 45 seconds on in-memory SQLite to a little over two
minutes. Worth it, because what it proves now is that the rules hold on the engine the product is
deployed on. A run interrupted before it can tidy up leaves a database behind; the next run sweeps
up anything older than six hours, so it is self-healing rather than something to remember.

---

## 7. Operating it

```bash
Get-Service Swarnakshi                 # is it up
Restart-Service Swarnakshi
Invoke-RestMethod http://localhost:6061/health
Get-EventLog -LogName Application -Source Swarnakshi -Newest 20
```

The service is set to restart twice on a crash (after 5s, then 20s) and then stay down, so a genuine
failure stays visible instead of looping forever.

### When something is wrong

| Symptom | Cause | Fix |
|---|---|---|
| Service starts then stops | Bad connection string, or `Jwt:Key` missing | Run `C:\Swarnakshi\app\Swarnakshi.Api.exe --migrate` by hand; it prints the real error |
| `Globalization Invariant Mode is not supported` | `InvariantGlobalization` was set back to `true` | It must stay `false` — SqlClient needs ICU |
| `Login failed for user 'SivayaanHMS'` | Password in the settings file no longer matches the login | Rotate both together, section 5 |
| First request after an idle period is slow | `AUTO_CLOSE` is on | `ALTER DATABASE SCOPS SET AUTO_CLOSE OFF` |
| A deep link 404s | `wwwroot` missing from the package | Re-run `Publish.ps1`; `Deploy.ps1` checks for this |
| `/api/...` returns HTML | Older build without the `/api` fallback | Redeploy |

---

## Appendix A — Hosting under IIS instead

The Windows service is the supported path because it needs nothing installed beyond the .NET runtime.
IIS is the alternative when you want IIS to terminate TLS and bind the certificate:

1. Install the **ASP.NET Core Hosting Bundle** (not installed by default; IIS alone is not enough)
   and `iisreset`.
2. Create an application pool with **No Managed Code**, and set its identity to an account that can
   read `appsettings.Production.json`.
3. Point a site at `C:\Swarnakshi\app`. `dotnet publish` already wrote the `web.config` that starts
   the app.
4. Set `ASPNETCORE_ENVIRONMENT=Production` on the pool, and remove `Urls` from the settings file —
   IIS supplies the binding.
5. Do not install the Windows service as well. Both would open the same database, which is supported,
   but two copies serving the same site is a confusion, not a feature.

## Appendix B — LocalDB is not a deployment target

`(localdb)\MSSQLLocalDB` is a developer convenience, and the application will run against it, but it
must not be the database a deployment points at:

- it starts on demand under **one Windows user's account** and shuts down after about 15 minutes idle;
- it does not start at boot;
- a Windows service running as LocalSystem **cannot reach** a LocalDB instance owned by an
  interactive user, so the deployed service would fail to connect no matter what the connection
  string says.

Use it for SSMS work and scratch databases. Deploy against `.\SQLEXPRESS`.
