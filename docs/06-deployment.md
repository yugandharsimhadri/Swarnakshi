# 06 — Build & Deployment

Swarnakshi runs as **one Windows service** talking to **one SQL Server Express database**. The
service is the API and it also serves the built React UI out of its own `wwwroot`, so the UI and the
API share an origin. There is no reverse proxy, no second web server, and no CORS in production —
one thing to install, one thing to start, one thing to watch.

| | |
|---|---|
| Database | SQL Server Express, instance `.\SQLEXPRESS`, database **SCOPS** |
| Database login | `SivayaanHMS` (SQL authentication) |
| Service | `Swarnakshi`, automatic start, runs as LocalSystem |
| Listens on | `http://0.0.0.0:8080` (change with `-Port`) |
| Installed at | `C:\Swarnakshi\app`, data under `C:\Swarnakshi\data`, backups in `C:\Swarnakshi\backups` |
| Health check | `http://localhost:8080/health` → `{"status":"ok"}` |

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

### 1.2 Create the database

From a checkout, as a Windows administrator (this connects with your own Windows login, which is a
sysadmin on a default Express install):

```bash
sqlcmd -S .\SQLEXPRESS -E -C -i deploy\sql\01-create-database.sql -v AppPassword="<database password>"
```

The script is idempotent — running it twice changes nothing. It creates:

- the **SCOPS** database, with `READ_COMMITTED_SNAPSHOT` on so a long posting transaction does not
  block every dashboard query behind it, `AUTO_CLOSE` off (Express defaults it on, which costs a
  slow first request after every idle period), and SIMPLE recovery;
- the **`SivayaanHMS`** login, if it does not already exist. It never resets an existing password —
  use `02-rotate-password.sql` for that;
- the matching database user, granted `db_datareader`, `db_datawriter`, `EXECUTE`, and the DDL
  rights EF Core migrations need. Deliberately **not** `db_owner`: the account can shape the schema
  it owns and cannot drop the database or manage other logins.

### 1.3 Build the package

On a build machine, or the server, from a clean checkout of the commit you intend to ship:

```bash
powershell -File deploy\scripts\Publish.ps1
```

It refuses to build from a dirty working tree (`-AllowDirty` for a throwaway test build), runs
`dotnet test` as the gate, builds the UI with `npm ci` into the API's `wwwroot`, publishes the API,
and writes `deploy\out\` with `app\`, `sql\`, `scripts\` and a `build.json` naming the commit.

Copy that folder to the server, for example to `C:\Swarnakshi\packages\<version>`.

### 1.4 Deploy

From inside the copied package, in an **elevated** PowerShell:

```bash
powershell -File .\scripts\Deploy.ps1 -DbPassword "<database password>"
```

This first run creates `C:\Swarnakshi\app\appsettings.Production.json` — the only file on the server
that holds secrets — installs the `Swarnakshi` service, applies the schema, starts it, and waits for
`/health`. The database password is needed only on this first run; later deployments leave the
settings file alone.

### 1.5 Open the port

```bash
New-NetFirewallRule -DisplayName "Swarnakshi 8080" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow
```

### 1.6 First login, and the one thing to do immediately

Browse to `http://<server>:8080/`.

| Account | Login | Purpose |
|---|---|---|
| Founding tenant owner | `owner@swarnakshi` | The company's first Owner |
| Platform operator | `EnterpriseAdmin` | Licence renewal and password resets only. Never sees company data. |

Both are seeded with the passwords in the settings file. **Change them at first login.** The seeder
sets a password only when it creates a row, so a changed password is never quietly reset by a
restart or a redeployment.

---

## 2. Every deployment after the first

```bash
powershell -File deploy\scripts\Publish.ps1        # on the build machine
# copy deploy\out to the server
powershell -File .\scripts\Deploy.ps1              # elevated, on the server
```

No password argument, no database script, no service installation — `Deploy.ps1` detects that the
settings file exists and treats the run as an upgrade. In order it:

1. **backs up SCOPS** and verifies the backup, labelled with the version being replaced;
2. **copies the current release to `C:\Swarnakshi\previous`**, so a rollback has a target;
3. stops the service;
4. swaps in the new binaries, keeping `appsettings.Production.json` and everything under
   `C:\Swarnakshi\data`. `wwwroot` is emptied rather than merged — a stale hashed asset left behind
   would be served to a browser that has just been handed a new `index.html`;
5. **applies migrations as an explicit step** (`Swarnakshi.Api.exe --migrate`), which exits non-zero
   on failure. This is why the schema change is not left to happen on first request: a bad migration
   fails the deployment while the service is still stopped, instead of taking the site down under
   traffic;
6. starts the service and polls `/health` until it answers;
7. and if anything from step 3 onward fails, puts the previous release back and starts it.

Expected downtime is the length of steps 3–6, normally under a minute.

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

`appsettings.json` ships in the package and holds no secrets. Everything sensitive lives in
`C:\Swarnakshi\app\appsettings.Production.json`, which `New-ProductionSettings.ps1` writes, locks to
SYSTEM and Administrators, and which is **git-ignored**. `deploy/appsettings.Production.template.json`
shows the shape.

| Key | Notes |
|---|---|
| `ConnectionStrings:Default` | SQL Server connection string |
| `Database:Provider` | `SqlServer` |
| `Database:CommandTimeoutSeconds` | 60 |
| `PlatformAdmin:Username` / `:Password` | EnterpriseAdmin seed credentials, used only when the row is created |
| `Jwt:Key` | ≥32 characters. The app refuses to start outside Development without it. |
| `Urls` | `http://0.0.0.0:8080` |
| `Cors:Origins` | Empty. The UI is same-origin; add an entry only if another site must call this API. |
| `Seed:Demo` | `false`. Demo data is Development-only and is ignored in Production regardless. |
| `Storage:LocalRoot` | Attachment directory, `C:\Swarnakshi\data\uploads` |

Any of these can be overridden by an environment variable using `__` for `:` —
`ConnectionStrings__Default`, `Jwt__Key`.

### Rotating secrets

```bash
sqlcmd -S .\SQLEXPRESS -E -C -i deploy\sql\02-rotate-password.sql -v NewPassword="<new>"
powershell -File .\scripts\New-ProductionSettings.ps1 -DbPassword "<new>" -KeepJwtKey
Restart-Service Swarnakshi
```

`-KeepJwtKey` matters. The JWT signing key is generated once and must stay put: every issued access
and refresh token is signed with it, so replacing it signs every user out. Rotating the database
password does not have to.

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

### The test suite still uses SQLite

`dotnet test` builds the whole schema in memory against SQLite in about a second, which is what keeps
245 tests at well under a minute. So the provider switch stays, and **nothing in the model or in any
query may depend on one provider's behaviour** — no provider-specific SQL or types, no filtered
unique indexes. Two places knowingly branch on the provider and are the only two allowed to:

- `AppDbContext.OnModelCreating` stores `DateTimeOffset` as UTC ticks on SQLite, which cannot order
  or compare the type natively. SQL Server gets a real `datetimeoffset(7)`.
- `PlatformSeeder.AdoptOrphanedRowsAsync` quotes identifiers through the provider's own
  `ISqlGenerationHelper` rather than by hand.

Because the tests do not exercise SQL Server, the real gate before a release is `Publish.ps1`
followed by a deploy to a scratch database — not the unit tests alone.

---

## 7. Operating it

```bash
Get-Service Swarnakshi                 # is it up
Restart-Service Swarnakshi
Invoke-RestMethod http://localhost:8080/health
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
