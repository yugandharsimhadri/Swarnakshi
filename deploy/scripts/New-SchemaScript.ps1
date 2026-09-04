<#
.SYNOPSIS
    Regenerates deploy\sql\03-schema.sql - the whole database schema as runnable SQL.

.DESCRIPTION
    For sites whose DBA applies schema changes by hand rather than letting the application do it.
    Publish.ps1 runs this, so every package ships a schema script matching the binaries beside it.

    The script it writes is idempotent: EF wraps each migration in a check against
    __EFMigrationsHistory, so running it twice is a no-op and running it against a database that is
    partly up to date applies only what is missing.

    Run it after adding a migration:

        powershell -File deploy\scripts\New-SchemaScript.ps1

.NOTES
    The header this prepends is not decoration. `sqlcmd` connects with QUOTED_IDENTIFIER OFF, and
    the schema has indexes that SQL Server refuses to create under that setting - without the SET
    the script dies on the first CREATE INDEX having built exactly one table. SSMS defaults it ON,
    so the failure only shows up on the command line, which is where a DBA would actually run this.
#>
[CmdletBinding()]
param(
    [string] $Output = (Join-Path $PSScriptRoot '..\sql\03-schema.sql')
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$body = Join-Path ([System.IO.Path]::GetTempPath()) "swk-schema-$([guid]::NewGuid().ToString('N')).sql"

Push-Location $repo
try {
    # A connection string has to be present for the design-time factory to build the context, but
    # --idempotent generates from the migrations in the assembly and never connects to it.
    $env:Database__Provider = 'SqlServer'
    if (-not $env:ConnectionStrings__Default) {
        $env:ConnectionStrings__Default = 'Server=.\SQLEXPRESS;Database=SCOPS;Trusted_Connection=True;TrustServerCertificate=True'
    }

    # 'dotnet ef' prints its progress to stderr, which Windows PowerShell turns into a terminating
    # NativeCommandError under $ErrorActionPreference = 'Stop'. The exit code is the real verdict.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        dotnet ef migrations script --idempotent `
            --project src\Swarnakshi.Infrastructure --startup-project src\Swarnakshi.Api `
            --output $body 2>&1 | Out-Host
    } finally { $ErrorActionPreference = $previous }
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef migrations script failed." }

    $header = @"
/*
    Swarnakshi - complete database schema.

    GENERATED FILE. Do not edit by hand: regenerate with
        powershell -File deploy\scripts\New-SchemaScript.ps1
    after adding an EF migration, or your edit is lost on the next build.

    Run it against a database that already exists (create it with 01-create-database.sql):

        sqlcmd -S .\SQLEXPRESS -E -C -b -d SCOPS -i 03-schema.sql

    Idempotent. Every migration is wrapped in a check against __EFMigrationsHistory, so running
    this twice does nothing the second time, and running it against a partly-migrated database
    applies only what is missing.

    Applying this by hand is optional. Deploy.ps1 applies the same migrations itself through
    Swarnakshi.Api.exe --migrate, and finding the work already done it simply reports the schema is
    up to date. Doing it here is for sites where only a DBA may change the schema - and it means
    the application login never needs CREATE TABLE or ALTER at all.

    It creates tables, indexes and foreign keys. It does NOT create master data: the platform
    operator, the founding company, expense heads, units and the material taxonomy are seeded in
    application code the first time the service starts, not here.

    Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') from commit $((git rev-parse --short HEAD).Trim())
*/

-- sqlcmd connects with QUOTED_IDENTIFIER OFF and SQL Server will not create this schema's indexes
-- under that setting. SSMS defaults it ON, so without these two lines the script works in SSMS and
-- dies on the command line after one table - which is the worst way for it to fail.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

"@

    $sql = Get-Content $body -Raw
    New-Item -ItemType Directory -Force -Path (Split-Path $Output) | Out-Null
    ($header + $sql) | Out-File -FilePath $Output -Encoding utf8

    $item = Get-Item $Output
    $tables = (Select-String -Path $Output -Pattern 'CREATE TABLE' -AllMatches).Count
    Write-Host ("Wrote {0} - {1:N0} bytes, {2} tables" -f $item.FullName, $item.Length, $tables) -ForegroundColor Green
}
finally {
    Remove-Item $body -ErrorAction SilentlyContinue
    Pop-Location
}
