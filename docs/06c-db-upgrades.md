# 06c — Database upgrades on a live server

The server is live. From here on, every release that changes the schema ships **one upgrade script**
covering only that release, and you choose how it is applied:

- **A —** the DBA runs the script against the database, then the API is restarted; or
- **B —** the API applies it itself on restart (`Swarnakshi.Api.exe --migrate`).

They do the same thing and are safe in either order, or both. Section 4 explains why.

For a **brand-new** server, none of this applies — run `01-create-database.sql` then
`03-schema.sql`, as in [06b-deployment-split.md](06b-deployment-split.md).

---

## 1. Find out what the live database is on

Before anything else, on the SQL Server:

```bash
sqlcmd -S .\SQLEXPRESS -E -C -d COPS -Q "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId"
```

The last row is the release the database is on. As of the deployment on the night of **4 September
2026** that is:

```
20260903165631_InitialCreate
20260904091235_PerformanceIndexes
```

Every upgrade script names the migration it starts from, in its header. If the bottom row here does
not match, you have the wrong script — stop and get the right one rather than running it anyway.

---

## 2. The upgrade for today's release — 5 September 2026

**File:** `deploy\sql\upgrades\2026-09-05-approval-gate.sql`
**From:** `20260904091235_PerformanceIndexes` → `20260905075817_ApprovalGate`

### What it changes

Seven columns on **one table**, `SupplierPayments`, plus a backfill of the rows already in it.
Nothing is dropped, nothing is renamed, no other table is touched, and no index is rebuilt.

| Column | Type | Why |
|---|---|---|
| `Status` | `int NOT NULL` default `0` | a supplier payment now waits for the Owner instead of applying itself |
| `ApprovedBy` | `uniqueidentifier NULL` | who approved it |
| `ApprovedAt` | `datetimeoffset NULL` | when |
| `Remarks` | `nvarchar(512) NULL` | the approver's note, or the auto-approval's |
| `ModifiedBy` / `ModifiedAt` | nullable | carried by every auditable row |
| `ConcurrencyToken` | `uniqueidentifier NOT NULL` | optimistic concurrency, as on every other transaction table |

### The backfill, and why it is not optional

```sql
UPDATE [SupplierPayments] SET [Status] = 6;      -- 6 = Posted
UPDATE [SupplierPayments] SET [ConcurrencyToken] = NEWID();
```

Every supplier payment recorded before this release had **already** been added to its invoice's
`PaidAmount` — that was the only way to record one. `Status` would otherwise default them all to
Draft, and the code that now recomputes `PaidAmount` from posted payments would reduce those
invoices to unpaid. If you apply the columns by hand rather than running the script, run these two
statements too.

### Running it

```bash
sqlcmd -S .\SQLEXPRESS -E -C -b -d COPS -i 2026-09-05-approval-gate.sql
```

`-b` matters: without it `sqlcmd` reports success even when a batch failed, and a half-applied
upgrade looks like a clean run.

**Downtime:** none required. `ALTER TABLE ADD` of nullable columns, and of a `NOT NULL` column with
a default, is a metadata-only change on SQL Server — it does not rewrite the table. The two
`UPDATE`s touch as many rows as you have supplier payments, which today is a handful. It is still
worth doing during the deployment window rather than mid-morning, because the moment it lands the
running API is one release behind the schema.

**Rollback:** restore the backup from step 3. The columns can be dropped by hand
(`ALTER TABLE [SupplierPayments] DROP COLUMN ...`) if you would rather, but the row in
`__EFMigrationsHistory` has to go with them or the application will believe the change is applied:

```sql
DELETE FROM [__EFMigrationsHistory] WHERE MigrationId = N'20260905075817_ApprovalGate';
```

### One thing that is *not* in the script

The new **auto-approve limit** setting is not written here. It does not need to be: when the row is
absent the application reads the limit as **0**, which means everything goes to the Owner — the
intended default. The Owner creates the row the first time they save on
**More → Your settings → Approvals**.

If you would rather see the row explicitly, insert one per company:

```sql
INSERT INTO [Settings] (Id, CompanyId, CreatedAt, IsDemo, [Key], [Value])
SELECT NEWID(), c.Id, SYSDATETIMEOFFSET(), 0, 'approvals.auto_approve_limit', '0'
FROM [Companies] c
WHERE NOT EXISTS (SELECT 1 FROM [Settings] s
                  WHERE s.CompanyId = c.Id AND s.[Key] = 'approvals.auto_approve_limit'
                        AND s.SiteId IS NULL);
```

The old `purchase.needs_approval` and `inventory.adjustment_needs_approval` rows are now ignored by
the application. Leave them; they are harmless, and deleting rows is not worth the risk of a typo in
a `WHERE` clause.

---

## 3. The order to do it in

```
1. Back up.          Backup-Database.ps1, or a plain BACKUP DATABASE. Verify it wrote a file.
2. Stop the API.     Stop-Service Swarnakshi   (or stop the IIS app pool)
3. Upgrade the DB.   sqlcmd ... -i 2026-09-05-approval-gate.sql        ← route A
4. Copy the build.   deploy\out\app\  over the app folder
5. Migrate.          Swarnakshi.Api.exe --migrate                      ← route B; exits 0
6. Start the API.    Start-Service Swarnakshi
7. Check.            curl https://copsapi.sivayaantechnologies.com/health
8. Upload the UI.    deploy\out\frontend\  to Cloudflare Pages
```

Step 3 and step 5 overlap on purpose. Run both: whichever goes first does the work and the other
finds it done. If your DBA will not run scripts, skip 3. If the application login has no `ALTER`
rights, skip 5 — and then step 4's build must not be started until 3 has succeeded.

`Deploy.ps1` already does 2 and 4–6. It does not do 1, 3, 7 or 8.

**Take the backup even for a metadata-only change.** The upgrade is small; the restore you cannot
do because nobody took one is not.

---

## 4. Why the two routes cannot disagree

Both come from the same EF migrations compiled into the same build. The script is generated from
them with `--idempotent`, which wraps every statement in

```sql
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'…')
```

so it is safe to run twice, safe to run against a database the API has already migrated, and safe to
run against one that is partway through. The application checks the same table on startup and
applies only what is missing. Neither can apply a migration the other has already applied, because
both read the same ledger.

This was verified rather than assumed. Against a copy of the database as it stood after last night's
deployment, carrying a supplier payment of ₹15,000 already applied to a ₹40,000 invoice:

- the script ran clean, twice (the second run changed nothing), leaving `Status = 6` and a real
  concurrency token;
- on a second identical copy, `--migrate` alone produced byte-for-byte the same result and exited 0.

An earlier draft of the script did **not** work, and the dry run is what found it. `dotnet ef
migrations script` emits the whole migration as one `sqlcmd` batch, and SQL Server compiles a batch
before running any of it — so the backfill, naming a column the `ALTER` two lines above had not yet
added, failed to parse with *Invalid column name 'Status'* after the columns had already been added.
The API route was unaffected, because it sends each statement separately. The backfill is now wrapped
in `EXEC(N'…')`, which defers compilation to execution time and behaves the same both ways.

The lesson generalises, and it is why section 5 exists: **a migration that carries data changes must
be dry-run as a script, not only through the API.**

---

## 5. Generating the script for the next release

After adding a migration:

```bash
powershell -File deploy\scripts\New-UpgradeScript.ps1 -From <the migration the live DB is on>
```

It writes `deploy\sql\upgrades\<date>-<name>.sql` with a header naming both ends of the jump.
`Publish.ps1` regenerates the full `03-schema.sql` for fresh installs; this is the companion for
servers already running.

Dry-run it before it goes anywhere near production. The whole rehearsal is four commands:

```bash
sqlcmd -S .\SQLEXPRESS -E -C -b -Q "CREATE DATABASE [COPS_Rehearsal]"
dotnet ef migrations script 0 <the migration the live DB is on> --idempotent --project src\Swarnakshi.Infrastructure --startup-project src\Swarnakshi.Api --output baseline.sql
sqlcmd -S .\SQLEXPRESS -E -C -b -d COPS_Rehearsal -i baseline.sql
sqlcmd -S .\SQLEXPRESS -E -C -b -d COPS_Rehearsal -i deploy\sql\upgrades\<the new script>.sql
```

Better still, restore last night's backup into `COPS_Rehearsal` instead of building the baseline from
migrations: then the rehearsal runs against the real data, which is where the surprises live. Drop
the rehearsal database afterwards.
