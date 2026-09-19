# 11 — Moving to PostgreSQL

The application now runs on PostgreSQL and nothing else. This page is the step-by-step for moving
the live server off SQL Server Express — written for the server as it actually is (section 2) —
and then the day-to-day afterwards.

Read it once end to end before starting. The move itself is about twenty minutes of downtime; the
things that go wrong are the ones skipped in the reading.

| | |
|---|---|
| Rehearsed on | this repository's own dev database: 42 tables, 21,525 rows, 8.5 s, every row count and every money total verified |
| Downtime | from stopping the old API to starting the new one — section 4, about twenty minutes |
| Rollback | the previous build against SQL Server, which the new build cannot read — section 6 |
| Configuration | the connection string in `appsettings.Production.json`, and nowhere else |

---

## 1. What changed, in one screen

**One provider.** `Npgsql.EntityFrameworkCore.PostgreSQL` replaces the SQL Server provider. There is
no switch and no second provider, for the same reason there was not one before: two providers is
two sets of behaviour to test and one of them is always the one nobody runs.

**snake_case names.** Tables and columns are `purchase_headers.total_amount`, not
`PurchaseHeaders.TotalAmount`. PostgreSQL folds unquoted identifiers to lower case, so keeping
PascalCase would have meant quoting every identifier in every query anyone ever typed by hand —
`"PurchaseHeaders"."TotalAmount"` — for the life of the system. The schema was being created from
scratch and the data migrator maps names from the model, so this was the one moment it was free.

**Fresh migrations.** The SQL Server migrations are gone and there is one `InitialPostgres`. They
were provider-specific SQL for a schema that no longer exists; the data moves by a different route
(section 4.3).

**Two behaviours that SQL Server gave away and PostgreSQL does not.** SQL Server's default
collation compares text case-insensitively; PostgreSQL does not. So every search lower-cases both
sides — searching for `cement` still finds `OPC 53 Grade Cement` — and every code a user types is
stored upper-case, so `gv-101` and `GV-101` remain the same villa. Both are spelled out in the
code now rather than inherited from a collation.

**UTC, still.** Every timestamp the application writes is UTC and always was. Npgsql *refuses* a
`DateTimeOffset` with any other offset, which is a stricter guarantee than SQL Server offered; the
migrator normalises anything it finds to UTC on the way across.

**Configuration is one line.** `ConnectionStrings:Default` in `appsettings.Production.json`. The
move to a cloud-hosted PostgreSQL later is that line plus `SSL Mode=Require` — section 7.

---

## 2. The setup this guide is written for

| | |
|---|---|
| Frontend | Cloudflare Pages at `cops.sivayaantechnologies.com` — **unchanged by this move** |
| API | IIS on your server, physical path `F:\sivayaan\copsapi`, reached through the Cloudflare tunnel as `copsapi.sivayaantechnologies.com` |
| Old database | SQL Server Express `.\SQLEXPRESS`, database `COPS`, login `SivayaanHMS` |
| New database | PostgreSQL on the same server, already installed; database `cops`, role `cops_app` |

The frontend never learns where the database is. It calls `copsapi.sivayaantechnologies.com`, which
is the same URL before and after, so there is nothing to upload to Cloudflare Pages. This is a
change to the API server only.

Everything below runs **on the server**, in an elevated PowerShell, unless it says otherwise.

---

## 3. Before the day — prepare, with the old system still running

None of this touches SQL Server. Do it days ahead.

### 3.1 Confirm PostgreSQL is ready

```bash
Get-Service postgresql* | Select-Object Name, Status, StartType
```

`Running`, and **Automatic** — not *Automatic (Delayed Start)*. The API starts at boot and waits up
to a minute for the database; delayed start can exceed that on a slow machine. You will need the
`postgres` superuser password set when PostgreSQL was installed, in step 3.3 and never again.

The installer does not put `psql` on `PATH`. The scripts find it themselves; for the commands on
this page, either add `C:\Program Files\PostgreSQL\<version>\bin` to `PATH` once or spell the full
path.

### 3.2 Build the package and copy it to the server

On the build machine:

```bash
powershell -File deploy\scripts\Publish.ps1 -ApiBaseUrl https://copsapi.sivayaantechnologies.com
```

Copy `deploy\out` to the server — say `F:\sivayaan\release`. It carries `app\` (the new build),
`sql\` (the PostgreSQL scripts), `scripts\`, and `tools\DataMigrator\` — the migrator published as
an executable, so the server needs no SDK.

### 3.3 Create the database and its role

As `postgres`. Choose a new password for `cops_app`; it goes into two files below.

```bash
psql -U postgres -h localhost -v DbName="cops" -v AppRole="cops_app" -v AppPassword="<new password>" -f F:\sivayaan\release\sql\01-create-database.sql
```

All three `-v` values are required and none has a default. It creates a role that can log in and
nothing else, and a database that role **owns** — which is what lets the application create its own
tables without being a superuser, and stops it touching any other database on the server. Safe to
re-run: a second run changes nothing and leaves the password alone. Lower-case names, kept that way.

### 3.4 Fill in the migrator's settings

In `F:\sivayaan\release\tools\DataMigrator\`, copy `migration.template.json` to `migration.json`:

```json
{
  "Source": "Server=.\\SQLEXPRESS;Database=COPS;User ID=SivayaanHMS;Password=<current SQL Server password>;TrustServerCertificate=True",
  "Target": "Host=localhost;Port=5432;Database=cops;Username=cops_app;Password=<password from 3.3>"
}
```

The source is only ever read. `migration.json` is git-ignored because it holds two passwords.

### 3.5 Prepare the new settings file — but do not install it yet

Copy the live `F:\sivayaan\copsapi\appsettings.Production.json` somewhere safe, for example
`F:\sivayaan\release\appsettings.Production.SQLSERVER.json`. **That copy is your rollback.**

Make a second copy, `F:\sivayaan\release\appsettings.Production.POSTGRES.json`, with **exactly one
line changed**:

```json
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=cops;Username=cops_app;Password=<password from 3.3>;Application Name=Swarnakshi"
}
```

`Jwt:Key`, `Cors:Origins`, `Logging`, `Storage` — all exactly as they were. **Do not generate a new
JWT key**: that signs every user out and they will tell you about it.

### 3.6 Check the migrator runs

```bash
F:\sivayaan\release\tools\DataMigrator\Swarnakshi.DataMigrator.exe --verify-only
```

With an empty target this reports every table as differing — that is expected, and not the point.
The point is that it starts, reads `migration.json`, and reaches both databases. A password or
firewall problem shows up here, days before it matters. (It needs the same .NET 10 runtime IIS
already uses; `Publish.ps1 -SelfContained` bundles it if the server has none.)

### 3.7 Find the app pool's name

```bash
Import-Module WebAdministration; Get-Website | Select-Object Name, ApplicationPool, PhysicalPath
```

The row whose physical path is `F:\sivayaan\copsapi` names the pool. It is `<pool>` in what
follows.

---

## 4. The day — about twenty minutes with the site down

### 4.1 Stop the API

The old API must stop before the copy, or a purchase entered during it exists in one database and
not the other and nobody can say which.

```bash
Import-Module WebAdministration; Stop-WebAppPool -Name "<pool>"
```

`cops.sivayaantechnologies.com` shows errors from this moment until 4.6.

### 4.2 The last SQL Server backup

The rollback point, and once SQL Server is decommissioned the only copy of it that will exist:

```bash
sqlcmd -S .\SQLEXPRESS -E -C -b -Q "BACKUP DATABASE [COPS] TO DISK='F:\sivayaan\backups\COPS-before-postgres.bak'"
```

### 4.3 Move the data

```bash
F:\sivayaan\release\tools\DataMigrator\Swarnakshi.DataMigrator.exe
```

It finds `migration.json` beside itself. What it does, and refuses to do:

1. **Applies the application's migrations** to the empty target, so the schema is exactly what the
   new build expects. It does *not* seed — the data about to arrive already contains everything
   the seeder would have created.
2. **Stops if the target holds any data.** This is a one-way, one-time copy into an empty schema.
   Running it twice is a mistake and is treated as one; to start over, drop and recreate the
   database with `01-create-database.sql`.
3. **Orders the tables parent-first** from the foreign keys in the model, and streams every row
   across with binary COPY, converting types on the way — `uniqueidentifier` to `uuid`,
   `datetimeoffset` to `timestamptz` in UTC, `date` to `date`, `decimal(18,2)` to `numeric(18,2)`.
4. **All of it in one PostgreSQL transaction.** A failure on the last table leaves the target
   exactly as empty as it was found.
5. **Verifies** that every table's row count matches and that the `SUM` of every numeric column
   matches — every amount, quantity, rate and balance in the system. A migration that "completed"
   but moved the wrong money is worse than one that failed, and this is the check that tells them
   apart.

You want to see:

```
   Every table's row count and every money column's total match. 8.8s.

Done. The application can now be started against the target.
```

If it ends with `VERIFICATION FAILED`, do not proceed. The transaction has already rolled back and
the target is empty. Read the differences it lists, fix the cause, run it again.

To re-run the verification alone at any later point — after the application has been running for a
while, say, to reassure yourself nothing was lost — `Swarnakshi.DataMigrator.exe --verify-only`
compares the two databases without writing anything. It will report differences for rows entered
since, which is expected.

### 4.4 Deploy the new build

```bash
Copy-Item F:\sivayaan\release\app\* F:\sivayaan\copsapi\ -Recurse -Force
```

### 4.5 Install the settings file from 3.5

```bash
Copy-Item F:\sivayaan\release\appsettings.Production.POSTGRES.json F:\sivayaan\copsapi\appsettings.Production.json -Force
```

This is the only line that changed. `Diagnose-Startup.ps1` and the backup script both read this
same file, so the one line is the one line everywhere.

### 4.6 Start, and check

```bash
Start-WebAppPool -Name "<pool>"
```

```bash
curl https://copsapi.sivayaantechnologies.com/health
```

Then sign in at `cops.sivayaantechnologies.com` and look at three screens: the dashboard (inventory
value and project cost should be what they were this morning), one villa's cost breakdown, and the
Approval Center. If any number differs from SQL Server's, go to section 6 — **do not start entering
data.**

The first start runs the seeders, which find every row already present and change nothing. The
startup log reads `Migrations applied and seed data verified`.

---

## 5. Afterwards

**Keep SQL Server running for two weeks**, untouched. It costs nothing and it is the only fallback
that needs no restore. After that, stop `MSSQL$SQLEXPRESS` and leave it stopped for two more weeks
before uninstalling anything.

**Schedule the new backup.** `Backup-Database.ps1` produces a `pg_dump` custom-format file and
finds the database through the settings file — through IIS, so with no arguments at all it finds
`F:\sivayaan\copsapi` on its own and writes to `F:\sivayaan\backups`. In Task Scheduler, nightly:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File F:\sivayaan\release\scripts\Backup-Database.ps1
```

Copy the `scripts\` folder somewhere permanent first — `F:\sivayaan\scripts` — so a later release
does not move it out from under the scheduled task.

**Delete `migration.json`** once the move is confirmed. It holds the old and the new passwords and
has no further purpose.

---

## 6. If it goes wrong: rolling back

The new build **cannot** talk to SQL Server. Rolling back is therefore the previous build *and* the
previous settings file:

```bash
Stop-WebAppPool -Name "<pool>"
```

1. Put the previous build's files back into `F:\sivayaan\copsapi\` (from wherever the last
   deployment's package is).
2. `Copy-Item F:\sivayaan\release\appsettings.Production.SQLSERVER.json F:\sivayaan\copsapi\appsettings.Production.json -Force`

```bash
Start-WebAppPool -Name "<pool>"
```

SQL Server was never written to after 4.1, so it is exactly as it was. Nothing entered into
PostgreSQL between 4.6 and the rollback comes back — which is the reason for looking at the three
screens in 4.6 *before* anybody enters anything.

---

## 7. Moving to the cloud, later

The application does not know or care where PostgreSQL is. Moving the database to a managed
service — Azure Database for PostgreSQL, Amazon RDS, Supabase, Neon, any of them — is:

1. Create the database and role there, with the provider's console or with
   `01-create-database.sql` against its host.
2. `Backup-Database.ps1` on the local server; `Restore-Database.ps1` pointed at a settings file
   naming the cloud host. A `pg_dump` is the same file on every PostgreSQL.
3. Change `ConnectionStrings:Default` to the cloud host, and add **`;SSL Mode=Require`** — the
   provider will insist, and traffic across a real network without it is readable.
4. Restart the app pool.

The one code decision worth revisiting then is the deliberate absence of `EnableRetryOnFailure` in
`Infrastructure/DependencyInjection.cs`. On one machine talking to its own database there is nothing
to retry; across a network there is, and the comment there explains what turning it on requires.

Where the *API* runs is unchanged by any of this — still IIS on your server behind the Cloudflare
tunnel until you decide otherwise, and that move is a separate page.

---

## 8. Day to day

| Task | Command (on the server) |
|---|---|
| Back up | `Backup-Database.ps1` — finds the app through IIS; or `-AppRoot F:\sivayaan\copsapi` |
| Restore | `Restore-Database.ps1 -BackupFile <file> -Confirm` — takes a safety backup first |
| Rotate the password | `psql -U postgres -h localhost -v AppRole=cops_app -v NewPassword="…" -f 02-rotate-password.sql`, then the settings file, then restart the pool |
| Apply a release's schema by hand | `psql -U cops_app -h localhost -d cops -v ON_ERROR_STOP=1 -1 -f <upgrade>.sql` — see [06c](06c-db-upgrades.md) |
| Trim the audit trail | `psql -U cops_app -h localhost -d cops -v KeepMonths=24 -f 05-purge-audit.sql` |
| Why won't it start | `Diagnose-Startup.ps1` — finds `F:\sivayaan\copsapi` through IIS |

Every script reads the connection string from `appsettings.Production.json`. There is no second
place to keep it in sync.

**Reading the data by hand.** Names are snake_case and unquoted:

```sql
SELECT p.txn_number, s.name AS supplier, p.total_amount, p.paid_amount, p.status
FROM purchase_headers p
JOIN suppliers s ON s.id = p.supplier_id
ORDER BY p.date DESC
LIMIT 20;
```

The rule for finding any name: the C# property `TotalAmount` is the column `total_amount`; the
entity `PurchaseHeader` is the table `purchase_headers`. The two exceptions are EF's own
`"__EFMigrationsHistory"`, which keeps its name and needs quoting, and the audit trail's `at`
column, which is the property `At`.

---

## 9. Running the tests

The test suite creates a throwaway database per run and needs a PostgreSQL role that may do so. It
reads **`testsettings.json`** at the repository root:

```bash
copy testsettings.template.json testsettings.json
```

then fill in the password. The file is git-ignored. The `postgres` superuser is the usual choice on
a developer machine. The same file serves the browser UAT suite.

For the API in development, the connection string lives in `dotnet user-secrets` as before:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=scops;Username=scops_app;Password=…" --project src/Swarnakshi.Api
```

---

## 10. What was checked before this page was written

- The full test suite, 292 tests, against PostgreSQL 18 — green on the first run after one fix:
  the seeder's raw SQL used the property name `CompanyId` where the column is now `company_id`.
  It now asks the model for the column name, which is the only place that can be right.
- The migrator against the repository's real development database: 42 tables, 21,525 rows,
  8.5 seconds, verified. The application then started against it, signed in with a migrated
  password hash, and a spot check of a setting and a money total matched SQL Server exactly.
- `01-create-database.sql`: creates, is a no-op on re-run, leaves the password alone, and exits 3
  when an argument is missing.
- `Backup-Database.ps1` and `Restore-Database.ps1`: a full round trip, with the safety backup the
  restore takes first, and the data intact afterwards.
