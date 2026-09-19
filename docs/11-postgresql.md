# 11 — Moving to PostgreSQL

The application now runs on PostgreSQL and nothing else. This page is the step-by-step for moving
the live server off SQL Server Express, and then the day-to-day afterwards.

Read it once end to end before starting. The move itself is about twenty minutes of downtime; the
things that go wrong are the ones skipped in the reading.

| | |
|---|---|
| Rehearsed on | this repository's own dev database: 42 tables, 21,525 rows, 8.5 s, every row count and every money total verified |
| Downtime | from stopping the old API to starting the new one — see step 4 |
| Rollback | the previous build against SQL Server, which the new build cannot read — see step 8 |
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
(section 4).

**Two behaviours that SQL Server gave away and PostgreSQL does not.** SQL Server's default
collation compares text case-insensitively; PostgreSQL does not. So every search lower-cases both
sides — searching for `cement` still finds `OPC 53 Grade Cement` — and every code a user types is
stored upper-case, so `gv-101` and `GV-101` remain the same villa. Both are spelled out in the
code now rather than inherited from a collation.

**UTC, still.** Every timestamp the application writes is UTC and always was. Npgsql *refuses* a
`DateTimeOffset` with any other offset, which is a stricter guarantee than SQL Server offered; the
migrator normalises anything it finds to UTC on the way across.

**Configuration is one line.** `ConnectionStrings:Default` in `appsettings.Production.json`. The
move to a cloud-hosted PostgreSQL later is that line plus `SSL Mode=Require` — section 9.

---

## 2. Before the day: prepare the server

Do all of this while the old system is still running. None of it touches SQL Server.

### 2.1 Install PostgreSQL

Download the Windows installer from postgresql.org (PostgreSQL 16 or later; this was built and
tested on 18). During the install:

- set a **password for the `postgres` superuser** and write it down — it is needed twice below
  and never again by the application;
- leave the port at **5432**;
- leave the locale at the default.

The installer does not put `psql` and `pg_dump` on `PATH`. The scripts find them under
`C:\Program Files\PostgreSQL\<version>\bin` on their own; to use them by hand, add that folder to
`PATH` once.

Confirm the service is running and set to start automatically:

```bash
Get-Service postgresql* | Select-Object Name, Status, StartType
```

It must be **Automatic**, not *Automatic (Delayed Start)* — the API starts at boot and waits up to
a minute for the database, and delayed start can exceed that on a slow machine.

### 2.2 Create the database and its role

As the `postgres` superuser, from the `deploy\sql` folder of the new build:

```bash
psql -U postgres -h localhost -v DbName="cops" -v AppRole="cops_app" -v AppPassword="<choose a password>" -f 01-create-database.sql
```

All three `-v` values are required and none has a default. It creates a role that can log in and
nothing else, and a database that role **owns** — which is what lets the application create its
own tables without being a superuser, and stops it touching any other database on the server. It
is idempotent: a second run changes nothing and leaves the password alone.

The name is lower-case on purpose. Keep it that way.

### 2.3 Fill in the migrator's settings

In the `tools\Swarnakshi.DataMigrator` folder, copy `migration.template.json` to `migration.json`
and fill in both connection strings:

```json
{
  "Source": "Server=.\\SQLEXPRESS;Database=COPS;User ID=SivayaanHMS;Password=<old password>;TrustServerCertificate=True",
  "Target": "Host=localhost;Port=5432;Database=cops;Username=cops_app;Password=<password from 2.2>"
}
```

`migration.json` is git-ignored because it holds two passwords. The source is only ever read.

### 2.4 Build

On the build machine, as always:

```bash
powershell -File deploy\scripts\Publish.ps1 -ApiBaseUrl https://copsapi.sivayaantechnologies.com
```

The package in `deploy\out` now carries the PostgreSQL `03-schema.sql`, the `psql`-based scripts,
and the migrator under `tools\`.

---

## 3. The day: stop writing to SQL Server

The old API must be stopped before the data is copied, or a purchase entered during the copy
exists in one database and not the other and nobody can say which.

```bash
Stop-Service Swarnakshi          # or: stop the IIS app pool
```

The UI on Cloudflare Pages will show errors from this moment until step 6. Twenty minutes, if the
reading was done.

Take one last SQL Server backup, with the old build's `Backup-Database.ps1` or by hand in SSMS.
This is the rollback point for section 8 and the only copy of SQL Server that will exist once it is
decommissioned.

---

## 4. Move the data

From the repository (or the package's `tools\` folder), with `migration.json` filled in:

```bash
dotnet run --project tools/Swarnakshi.DataMigrator
```

What it does, and refuses to do:

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

Expected output ends with:

```
   Every table's row count and every money column's total match. 8.8s.

Done. The application can now be started against the target.
```

If it ends with `VERIFICATION FAILED`, do not proceed. The transaction has already rolled back;
the target is empty. Read the differences it lists, fix the cause, run it again.

To re-run the verification alone at any later point — after the application has been running for a
while, say, to reassure yourself nothing was lost — `--verify-only` compares the two databases
without writing anything. It will report differences for rows entered since, which is expected.

---

## 5. Point the application at PostgreSQL

Deploy the new build as usual (`Deploy.ps1`, or copy `deploy\out\app` over the IIS folder — see
[06b](06b-deployment-split.md)). Then edit **`appsettings.Production.json`**:

```json
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=cops;Username=cops_app;Password=<password from 2.2>;Application Name=Swarnakshi"
}
```

This is the only line that changes. Everything else in the file — the JWT key, the CORS origins,
the log directory — stays exactly as it was. **Do not regenerate the JWT key**: that signs every
user out.

`Deploy.ps1` checks the connection string can open the database before it swaps the build in, and
`Diagnose-Startup.ps1` reads the same file, so the one line is checked by three things.

---

## 6. Start, and check

```bash
Start-Service Swarnakshi         # or: start the IIS app pool
```

```bash
curl https://copsapi.sivayaantechnologies.com/health
```

Then sign in from the UI and look at three screens: the dashboard (inventory value and project cost
should be what they were this morning), one villa's cost breakdown, and the Approval Center. If any
number differs from SQL Server's, stop and read section 8 — do not start entering data.

The first start also runs the seeders, which find every row already present and change nothing.
The startup log shows `Migrations applied and seed data verified`.

---

## 7. Afterwards

**Keep SQL Server running for two weeks**, untouched. It costs nothing and it is the only fallback
that needs no restore. After that, stop the `MSSQL$SQLEXPRESS` service and leave it stopped for
another two weeks before uninstalling anything.

**Schedule the new backup.** `Backup-Database.ps1` now produces a `pg_dump` custom-format file
(`cops-<stamp>-scheduled.dump`) and reads which database from `appsettings.Production.json`.
Put it in Task Scheduler nightly, as before. A backup that only exists on deployment days is not a
backup.

**Delete `migration.json`** once the move is confirmed. It holds the old and the new passwords and
has no further purpose.

---

## 8. If it goes wrong: rolling back

The new build **cannot** talk to SQL Server. Rolling back is therefore not a connection-string
change; it is the previous build *and* the previous connection string:

1. Stop the API.
2. Put the previous build back — `Deploy.ps1` keeps it in `previous\`, or use the package from the
   last deployment.
3. Restore the previous `appsettings.Production.json` (the previous build's folder has it), which
   points at SQL Server.
4. Start the API.

SQL Server was never written to after step 3, so it is exactly as it was. Nothing entered into
PostgreSQL between step 6 and the rollback comes back — which is the reason for looking at the
three screens in step 6 *before* anybody enters anything.

---

## 9. Moving to the cloud, later

The application does not know or care where PostgreSQL is. Moving the database to a managed
service — Azure Database for PostgreSQL, Amazon RDS, Supabase, Neon, any of them — is:

1. Create the database and role there, with the provider's console or with
   `01-create-database.sql` against its host.
2. `Backup-Database.ps1` on the local server; `Restore-Database.ps1` pointed at a settings file
   naming the cloud host. A `pg_dump` is the same file on every PostgreSQL.
3. Change `ConnectionStrings:Default` to the cloud host, and add **`;SSL Mode=Require`** — the
   provider will insist, and traffic across a real network without it is readable.
4. Restart the API.

The one code decision worth revisiting then is the deliberate absence of `EnableRetryOnFailure` in
`Infrastructure/DependencyInjection.cs`. On one machine talking to its own database there is nothing
to retry; across a network there is, and the comment there explains what turning it on requires.

Where the *application* runs is unchanged by any of this — it is still the local server behind
the Cloudflare tunnel until you decide otherwise, and that move is a separate page.

---

## 10. Day to day

| Task | Command |
|---|---|
| Back up | `Backup-Database.ps1 -AppRoot C:\Swarnakshi` |
| Restore | `Restore-Database.ps1 -AppRoot C:\Swarnakshi -BackupFile <file> -Confirm` |
| Rotate the password | `psql -U postgres -h localhost -v AppRole=cops_app -v NewPassword="…" -f 02-rotate-password.sql`, then the settings file, then restart |
| Apply a release's schema by hand | `psql -U cops_app -h localhost -d cops -v ON_ERROR_STOP=1 -1 -f <upgrade>.sql` — see [06c](06c-db-upgrades.md) |
| Trim the audit trail | `psql -U cops_app -h localhost -d cops -v KeepMonths=24 -f 05-purge-audit.sql` |
| Why won't it start | `Diagnose-Startup.ps1` |

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

## 11. Running the tests

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

## 12. What was checked before this page was written

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
