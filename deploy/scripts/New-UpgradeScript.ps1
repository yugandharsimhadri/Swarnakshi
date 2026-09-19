<#
.SYNOPSIS
    Writes the SQL that takes a LIVE database from the release it is on to the one being deployed.

.DESCRIPTION
    03-schema.sql builds the whole schema and is what a fresh install runs. This writes the much
    smaller script for a server that is already running: only the migrations added since -From.

    Both are idempotent - EF guards every migration with a check against __EFMigrationsHistory - so
    either one is safe to run twice, and safe to run after the application has already migrated
    itself. The difference is what a DBA has to read before signing it off: forty tables, or the
    six columns this release actually adds.

    Applying it is optional. `Swarnakshi.Api.exe --migrate` applies exactly the same migrations at
    deployment time, and Deploy.ps1 runs that step before the site is swapped in. Generate this for
    sites where only a DBA may change the schema, or when someone wants to read the change first.

.PARAMETER From
    The last migration the live database has. Get it from the server:

        psql -U cops_app -h localhost -d cops -t -A -c "SELECT migration_id FROM \"__EFMigrationsHistory\" ORDER BY migration_id DESC LIMIT 1"

.EXAMPLE
    powershell -File deploy\scripts\New-UpgradeScript.ps1 -From 20260904091235_PerformanceIndexes
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $From,
    [string] $To = '',
    [string] $Output = ''
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$body = Join-Path ([System.IO.Path]::GetTempPath()) "swk-upgrade-$([guid]::NewGuid().ToString('N')).sql"

Push-Location $repo
try {
    # A connection string has to be present for the design-time factory to build the context, but
    # generating a script works from the migrations in the assembly and never connects to it.
    if (-not $env:ConnectionStrings__Default) {
        $env:ConnectionStrings__Default = 'Host=localhost;Port=5432;Database=swarnakshi_design;Username=postgres;Password=postgres'
    }

    # 'dotnet ef' writes progress to stderr, which Windows PowerShell turns into a terminating error
    # under $ErrorActionPreference = 'Stop' even on success. The exit code is the real verdict.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $args = @(
            'ef', 'migrations', 'script', $From
            if ($To) { $To }
            '--idempotent'
            '--project', 'src\Swarnakshi.Infrastructure'
            '--startup-project', 'src\Swarnakshi.Api'
            '--output', $body
        )
        & dotnet @args 2>&1 | Out-Host
    } finally { $ErrorActionPreference = $previous }
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef migrations script failed." }

    $target = if ($To) { $To } else { 'the current build' }
    if (-not $Output) {
        $name = if ($To) { $To } else {
            # Name the file after the newest migration in the project, so two upgrade scripts from
            # different releases never collide in the folder.
            (Get-ChildItem (Join-Path $repo 'src\Swarnakshi.Infrastructure\Persistence\Migrations') -Filter '*.cs' |
                Where-Object { $_.Name -notmatch '\.Designer\.cs$|ModelSnapshot' } |
                Sort-Object Name | Select-Object -Last 1).BaseName
        }
        $slug = ($name -replace '^\d+_', '') -creplace '(?<!^)([A-Z])', '-$1'
        $Output = Join-Path $repo ("deploy\sql\upgrades\{0}-{1}.sql" -f (Get-Date -Format 'yyyy-MM-dd'), $slug.ToLowerInvariant())
    }

    $header = @"
/*
    Swarnakshi - database upgrade.

    GENERATED FILE. Regenerate with
        powershell -File deploy\scripts\New-UpgradeScript.ps1 -From $From

    From: $From
    To:   $target

    Run it against the LIVE database, as the application's role, before or during the deployment
    of the matching build:

        psql -U cops_app -h localhost -d cops -v ON_ERROR_STOP=1 -1 -f $(Split-Path -Leaf $Output)

    ON_ERROR_STOP matters: without it psql carries on past a failed statement and a half-applied
    upgrade looks like a clean run. -1 wraps the whole file in one transaction, so it is all or
    nothing.

    Idempotent, and it agrees with the application. Every statement is wrapped in a check against
    __EFMigrationsHistory, so running it twice does nothing the second time - and if the API has
    already migrated itself on restart, this finds the work done and changes nothing. Running both
    is not a mistake; it is the intended belt and braces.

    Schema only. No master data: settings, expense heads, units and the material taxonomy are
    seeded by the application on first start, not here.

    Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') from commit $((git rev-parse --short HEAD).Trim())
*/

"@

    $sql = Get-Content $body -Raw
    New-Item -ItemType Directory -Force -Path (Split-Path $Output) | Out-Null
    ($header + $sql) | Out-File -FilePath $Output -Encoding utf8

    $item = Get-Item $Output
    Write-Host ("Wrote {0} - {1:N0} bytes" -f $item.FullName, $item.Length) -ForegroundColor Green
}
finally {
    Remove-Item $body -ErrorAction SilentlyContinue
    Pop-Location
}
