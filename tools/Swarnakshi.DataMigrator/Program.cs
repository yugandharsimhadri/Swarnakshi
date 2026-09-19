using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using NpgsqlTypes;
using Swarnakshi.Infrastructure;
using Swarnakshi.Infrastructure.Persistence;

// ============================================================================================
//  Swarnakshi.DataMigrator — copies every row from the SQL Server database into PostgreSQL.
//
//  Usage:
//    Copy migration.template.json to migration.json beside this file, fill in the two connection
//    strings, then:
//
//        dotnet run --project tools/Swarnakshi.DataMigrator              # migrate
//        dotnet run --project tools/Swarnakshi.DataMigrator -- --verify-only   # re-check later
//
//    --config <path> points at a settings file somewhere else. migration.json is git-ignored:
//    it holds two passwords.
//
//  What it does, in order:
//    1. Applies the application's migrations to the target, so the schema is exactly what the
//       application expects. Nothing else is written — no seeding — because the data about to
//       arrive already contains everything the seeder would have created.
//    2. Refuses to continue if the target already holds data. This is a one-way, one-time move;
//       running it twice would be a mistake and is treated as one.
//    3. Reads the EF model for the list of tables, their columns and the foreign keys between
//       them, and orders the tables so that every parent is loaded before its children.
//    4. Streams each table from SQL Server into PostgreSQL with binary COPY, converting types
//       on the way. Everything runs in ONE PostgreSQL transaction: a failure on the last table
//       leaves the target exactly as empty as it was found.
//    5. Verifies: every table's row count matches, and the SUM of every numeric column matches.
//       A migration that "completed" but moved the wrong money is worse than one that failed.
//
//  Table and column names are never typed in here. The source names are the DbSet property
//  names and CLR property names (what the SQL Server schema was generated from); the target
//  names come from the model with snake_case applied (what the PostgreSQL schema was generated
//  from). Adding an entity to the model adds it to the migration automatically.
// ============================================================================================

var (source, target, verifyOnly) = ParseArgs(args);

var sw = Stopwatch.StartNew();
var options = new DbContextOptionsBuilder<AppDbContext>();
DependencyInjection.Configure(options, target);
await using var db = new AppDbContext(options.Options);

if (!verifyOnly)
{
    Console.WriteLine("1. Schema: applying the application's migrations to the target…");
    await db.Database.MigrateAsync();
}

var tables = Plan(db);
Console.WriteLine($"   {tables.Count} tables, ordered parent-first.");

await using var pg = new NpgsqlConnection(target);
await pg.OpenAsync();
await using var sql = new SqlConnection(source);
await sql.OpenAsync();

if (!verifyOnly)
{
    Console.WriteLine("2. Checking the target is empty…");
    foreach (var t in tables)
    {
        var n = await CountAsync(pg, $"SELECT COUNT(*) FROM {Q(t.TargetTable)}");
        if (n > 0)
            Fail($"Target table {t.TargetTable} already holds {n:N0} rows. This tool moves data into an EMPTY "
               + "schema, once. If this is a retry after a failure, the previous run rolled back and the table "
               + "should be empty — check you are pointing at the right database. If you mean to start over, "
               + "drop and recreate the target database first.");
    }

    Console.WriteLine("3. Copying…");
    await using var tx = await pg.BeginTransactionAsync();
    long total = 0;
    foreach (var t in tables)
    {
        var rows = await CopyTableAsync(sql, pg, t);
        total += rows;
        Console.WriteLine($"   {t.SourceTable,-28} -> {t.TargetTable,-30} {rows,10:N0} rows");
    }
    await tx.CommitAsync();
    Console.WriteLine($"   {total:N0} rows in {sw.Elapsed.TotalSeconds:N1}s. Committed.");
}

Console.WriteLine(verifyOnly ? "Verifying…" : "4. Verifying…");
var problems = new List<string>();
foreach (var t in tables)
{
    var srcCount = await CountAsync(sql, $"SELECT COUNT(*) FROM [{t.SourceTable}]");
    var dstCount = await CountAsync(pg, $"SELECT COUNT(*) FROM {Q(t.TargetTable)}");
    if (srcCount != dstCount)
        problems.Add($"{t.TargetTable}: {srcCount:N0} rows at source, {dstCount:N0} at target");

    foreach (var c in t.Columns.Where(c => c.ClrType == typeof(decimal) || c.ClrType == typeof(decimal?)))
    {
        var srcSum = await SumAsync(sql, $"SELECT COALESCE(SUM([{c.SourceName}]), 0) FROM [{t.SourceTable}]");
        var dstSum = await SumAsync(pg, $"SELECT COALESCE(SUM({Q(c.TargetName)}), 0) FROM {Q(t.TargetTable)}");
        if (srcSum != dstSum)
            problems.Add($"{t.TargetTable}.{c.TargetName}: sums differ — {srcSum:N2} at source, {dstSum:N2} at target");
    }
}

if (problems.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"VERIFICATION FAILED — {problems.Count} difference(s):");
    foreach (var p in problems) Console.WriteLine("   " + p);
    Console.WriteLine();
    Console.WriteLine("Do not point the application at this database. Drop it, fix the cause, and run again.");
    return 2;
}

Console.WriteLine($"   Every table's row count and every money column's total match. {sw.Elapsed.TotalSeconds:N1}s.");
Console.WriteLine();
Console.WriteLine("Done. The application can now be started against the target.");
return 0;

// ---- planning ------------------------------------------------------------------------------

static List<TablePlan> Plan(AppDbContext db)
{
    // DbSet property name = the table name the SQL Server schema was generated with. The model's
    // own GetTableName() now answers with the snake_case PostgreSQL name.
    var sourceTableByClr = typeof(AppDbContext).GetProperties()
        .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
        .ToDictionary(p => p.PropertyType.GetGenericArguments()[0], p => p.Name);

    var entities = db.Model.GetEntityTypes()
        .Where(e => !e.IsOwned() && e.GetTableName() is not null)
        .ToList();

    var plans = entities.Select(e =>
    {
        if (!sourceTableByClr.TryGetValue(e.ClrType, out var sourceTable))
            throw new InvalidOperationException(
                $"{e.ClrType.Name} has no DbSet on AppDbContext, so its SQL Server table name is unknown.");

        var columns = e.GetProperties()
            .Select(p => new ColumnPlan(p.Name, p.GetColumnName(), p.ClrType))
            .ToList();

        var parents = e.GetForeignKeys()
            .Select(fk => fk.PrincipalEntityType)
            .Where(principal => principal != e)          // self-references need no ordering between tables
            .Distinct()
            .ToList();

        return new TablePlan(e, sourceTable, e.GetTableName()!, columns, parents);
    }).ToList();

    // Kahn's algorithm: emit a table only when every parent it points at has been emitted.
    var byEntity = plans.ToDictionary(p => p.Entity);
    var ordered = new List<TablePlan>();
    var done = new HashSet<IEntityType>();
    while (ordered.Count < plans.Count)
    {
        var ready = plans.Where(p => !done.Contains(p.Entity) && p.Parents.All(done.Contains)).ToList();
        if (ready.Count == 0)
        {
            var stuck = string.Join(", ", plans.Where(p => !done.Contains(p.Entity)).Select(p => p.TargetTable));
            throw new InvalidOperationException($"Circular foreign keys between: {stuck}. Load order cannot be decided.");
        }
        foreach (var p in ready.OrderBy(p => p.TargetTable)) { ordered.Add(p); done.Add(p.Entity); }
    }
    return ordered;
}

// ---- copying -------------------------------------------------------------------------------

static async Task<long> CopyTableAsync(SqlConnection sql, NpgsqlConnection pg, TablePlan t)
{
    var select = "SELECT " + string.Join(", ", t.Columns.Select(c => $"[{c.SourceName}]")) + $" FROM [{t.SourceTable}]";
    var copy = $"COPY {Q(t.TargetTable)} (" + string.Join(", ", t.Columns.Select(c => Q(c.TargetName))) + ") FROM STDIN (FORMAT BINARY)";

    await using var read = new SqlCommand(select, sql) { CommandTimeout = 600 };
    await using var reader = await read.ExecuteReaderAsync();
    await using var writer = await pg.BeginBinaryImportAsync(copy);

    long rows = 0;
    while (await reader.ReadAsync())
    {
        await writer.StartRowAsync();
        for (var i = 0; i < t.Columns.Length; i++)
        {
            if (reader.IsDBNull(i)) { await writer.WriteNullAsync(); continue; }
            await WriteAsync(writer, reader.GetValue(i), t.Columns[i]);
        }
        rows++;
    }
    await writer.CompleteAsync();
    return rows;
}

/// <summary>
/// One value across the boundary. Every branch here is a difference between the two engines
/// that would otherwise surface as a failed COPY, or worse, a silently wrong value.
/// </summary>
static async Task WriteAsync(NpgsqlBinaryImporter w, object value, ColumnPlan c)
{
    var clr = Nullable.GetUnderlyingType(c.ClrType) ?? c.ClrType;

    switch (value)
    {
        case Guid g:
            await w.WriteAsync(g, NpgsqlDbType.Uuid);
            break;

        case DateTimeOffset dto:
            // The one conversion that matters. Npgsql refuses a DateTimeOffset whose offset is not
            // zero — "only UTC is supported for timestamptz". The application always wrote UTC, but
            // a row touched by hand in SSMS may carry +05:30, and normalising here loses nothing:
            // an instant is an instant whatever offset it was spelled with.
            await w.WriteAsync(dto.ToUniversalTime(), NpgsqlDbType.TimestampTz);
            break;

        case DateTime dt:
            // SqlClient reads a SQL Server `date` as DateTime; the target column is a PostgreSQL
            // date and the model says DateOnly.
            if (clr == typeof(DateOnly))
                await w.WriteAsync(DateOnly.FromDateTime(dt), NpgsqlDbType.Date);
            else
                await w.WriteAsync(DateTime.SpecifyKind(dt, DateTimeKind.Utc), NpgsqlDbType.TimestampTz);
            break;

        case decimal d:
            await w.WriteAsync(d, NpgsqlDbType.Numeric);
            break;

        case int n:
            // Enums are stored as int on both sides; the model's CLR type says which, and it does
            // not change what is written.
            await w.WriteAsync(n, NpgsqlDbType.Integer);
            break;

        case long l:
            await w.WriteAsync(l, NpgsqlDbType.Bigint);
            break;

        case bool b:
            await w.WriteAsync(b, NpgsqlDbType.Boolean);
            break;

        case string s:
            await w.WriteAsync(s, NpgsqlDbType.Text);
            break;

        case byte[] bytes:
            await w.WriteAsync(bytes, NpgsqlDbType.Bytea);
            break;

        default:
            throw new InvalidOperationException(
                $"No conversion for {value.GetType().Name} in column {c.SourceName} (model type {c.ClrType.Name}).");
    }
}

// ---- helpers -------------------------------------------------------------------------------

static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

static async Task<long> CountAsync(System.Data.Common.DbConnection cn, string sqlText)
{
    await using var cmd = cn.CreateCommand();
    cmd.CommandText = sqlText;
    return Convert.ToInt64(await cmd.ExecuteScalarAsync());
}

static async Task<decimal> SumAsync(System.Data.Common.DbConnection cn, string sqlText)
{
    await using var cmd = cn.CreateCommand();
    cmd.CommandText = sqlText;
    return Convert.ToDecimal(await cmd.ExecuteScalarAsync());
}

static void Fail(string message)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("STOPPED: " + message);
    Environment.Exit(1);
}

/// <summary>
/// Settings come from migration.json — a file, because that is where every other connection
/// string in this system lives, and a command line with two passwords on it ends up in a shell
/// history. The file is looked for beside the executable and then in each parent folder, so
/// `dotnet run` from the repository root finds the one next to the project.
/// </summary>
static (string Source, string Target, bool VerifyOnly) ParseArgs(string[] args)
{
    string? configPath = null;
    var verifyOnly = false;
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--config": configPath = args[++i]; break;
            case "--verify-only": verifyOnly = true; break;
            default: Fail($"Unknown argument '{args[i]}'. Expected [--config <migration.json>] [--verify-only]."); break;
        }
    }

    configPath ??= FindUpwards("migration.json");
    if (configPath is null || !File.Exists(configPath))
        Fail("No migration.json found. Copy tools/Swarnakshi.DataMigrator/migration.template.json to "
           + "migration.json in the same folder and fill in the two connection strings, or pass --config <path>.");

    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configPath!));
    var root = doc.RootElement;
    var source = root.TryGetProperty("Source", out var s) ? s.GetString() : null;
    var target = root.TryGetProperty("Target", out var t) ? t.GetString() : null;
    if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        Fail($"{configPath} must contain both \"Source\" (SQL Server) and \"Target\" (PostgreSQL) connection strings.");
    if (source!.Contains("CHANGE_ME") || target!.Contains("CHANGE_ME"))
        Fail($"{configPath} still holds the template's CHANGE_ME placeholder.");

    Console.WriteLine($"Settings: {configPath}");
    return (source, target, verifyOnly);
}

static string? FindUpwards(string fileName)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, fileName);
        if (File.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    return null;
}

sealed record ColumnPlan(string SourceName, string TargetName, Type ClrType);

sealed record TablePlan(IEntityType Entity, string SourceTable, string TargetTable, List<ColumnPlan> ColumnList, List<IEntityType> Parents)
{
    public ColumnPlan[] Columns { get; } = ColumnList.ToArray();
}
