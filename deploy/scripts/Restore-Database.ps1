<#
.SYNOPSIS
    Restores the application's PostgreSQL database from a backup file.

.DESCRIPTION
    This overwrites the live database. Everything entered since the backup was taken is lost.
    It is the last option, not the first - read docs/06-deployment.md, "Rolling back", before
    running it, and stop the application first so nothing is writing during the restore.

    The script refuses to run unless -Confirm is given, and it takes a safety backup of the
    current state first, so a mistaken restore is itself recoverable.

    Which database comes from appsettings.Production.json under -AppRoot, the same place the
    application and Backup-Database.ps1 read it. The restore runs as the application's own role,
    which owns the database and can therefore drop and recreate everything in it - no superuser
    is needed.

.EXAMPLE
    Stop-Service Swarnakshi          # or stop the IIS app pool
    .\Restore-Database.ps1 -BackupFile C:\Swarnakshi\backups\cops-20260919-081500-pre-deploy.dump -Confirm
    Start-Service Swarnakshi
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $BackupFile,
    [string] $AppRoot = 'C:\Swarnakshi',
    [string] $PgBin   = '',
    [switch] $Confirm
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PostgresSettings.ps1')

if (-not (Test-Path $BackupFile)) { throw "No such backup file: $BackupFile" }
if (-not $Confirm) {
    throw "This REPLACES the live database with $BackupFile and loses everything entered since. Re-run with -Confirm if that is what you mean."
}

$conn = Read-PostgresConnection -AppRoot $AppRoot
$pgRestore = Find-PgTool -Name 'pg_restore' -PgBin $PgBin

# Safety net first. If the restore turns out to be the mistake, this is the way back.
Write-Host "Taking a safety backup of the current state first..."
& (Join-Path $PSScriptRoot 'Backup-Database.ps1') -AppRoot $AppRoot -Label 'before-restore' -PgBin $PgBin

Write-Host "Restoring $BackupFile into $($conn.Database) on $($conn.Host)..."
$env:PGPASSWORD = $conn.Password
try {
    # --clean --if-exists: drop every object in the dump before recreating it, so the database
    # ends up as the backup describes and not as a merge of the two. --single-transaction: all or
    # nothing - a restore that fails halfway leaves the database as it was, not half-replaced.
    # --no-owner/--no-privileges: ownership stays with the role doing the restore, which is the
    # application's, whatever it was on the machine the dump came from.
    & $pgRestore --host $conn.Host --port $conn.Port --username $conn.Username --dbname $conn.Database `
                 --clean --if-exists --single-transaction --no-owner --no-privileges --exit-on-error $BackupFile
    if ($LASTEXITCODE -ne 0) { throw "pg_restore exited with $LASTEXITCODE. The database was NOT changed (single transaction)." }
} finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

Write-Host "Restored. Start the application and check /health." -ForegroundColor Green
