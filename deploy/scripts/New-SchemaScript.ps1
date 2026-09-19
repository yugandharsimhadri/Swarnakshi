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
    Run the output with psql as the application role, with ON_ERROR_STOP set: without it psql
    carries on past a failed statement and a half-built schema looks like a clean run.
#>
[CmdletBinding()]
param(
    [string] $Output = ''     # resolved in the body: $PSScriptRoot is empty in param defaults under 5.1 -File
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not $Output) { $Output = Join-Path $PSScriptRoot '..\sql\03-schema.sql' }
$body = Join-Path ([System.IO.Path]::GetTempPath()) "swk-schema-$([guid]::NewGuid().ToString('N')).sql"

Push-Location $repo
try {
    # A connection string has to be present for the design-time factory to build the context, but
    # --idempotent generates from the migrations in the assembly and never connects to it.
    if (-not $env:ConnectionStrings__Default) {
        $env:ConnectionStrings__Default = 'Host=localhost;Port=5432;Database=swarnakshi_design;Username=postgres;Password=postgres'
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

        psql -U cops_app -h localhost -d cops -v ON_ERROR_STOP=1 -1 -f 03-schema.sql

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
