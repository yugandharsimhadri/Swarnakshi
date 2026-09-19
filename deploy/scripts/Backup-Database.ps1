<#
.SYNOPSIS
    Takes a compressed full backup of the application's PostgreSQL database and prunes old ones.

.DESCRIPTION
    Deploy.ps1 calls this before every deployment, so there is always a restore point taken
    minutes before the change that might need undoing. Schedule it nightly as well -- a backup
    that only exists on deployment days is not a backup.

    Which database, and how to reach it, comes from the application's own settings file
    (appsettings.Production.json under -AppRoot). One place for the connection string, so a
    backup can never quietly be taken of the wrong database.

    The dump is pg_dump's custom format (-Fc): compressed, and restorable table-by-table with
    pg_restore. Unlike a SQL Server .bak it is taken by THIS script's user over a normal
    connection, so no service-account permissions on the folder are involved.

.EXAMPLE
    .\Backup-Database.ps1
    .\Backup-Database.ps1 -Label 'before-v2' -KeepDays 90
#>
[CmdletBinding()]
param(
    [string] $AppRoot    = '',       # blank: found through IIS, then C:\Swarnakshi (PostgresSettings.ps1)
    [string] $BackupPath = '',       # blank: a backups\ folder beside the app
    [string] $Label      = 'scheduled',
    [int]    $KeepDays   = 30,
    [string] $PgBin      = ''        # folder holding pg_dump.exe; found automatically when blank
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PostgresSettings.ps1')

$conn = Read-PostgresConnection -AppRoot $AppRoot
$pgDump = Find-PgTool -Name 'pg_dump' -PgBin $PgBin
# Beside the app rather than inside it: a deployment replaces the app folder, and the backups
# taken before it should outlive it. For F:\sivayaan\copsapi that is F:\sivayaan\backups.
if (-not $BackupPath) { $BackupPath = Join-Path (Split-Path (Split-Path $conn.File -Parent) -Parent) 'backups' }
if (-not (Test-Path $BackupPath)) { New-Item -ItemType Directory -Force -Path $BackupPath | Out-Null }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$file  = Join-Path $BackupPath "$($conn.Database)-$stamp-$Label.dump"

Write-Host "Backing up $($conn.Database) on $($conn.Host):$($conn.Port) to $file"

# The password goes to pg_dump through its environment, never on the command line, so it does
# not appear in the process list or the shell history.
$env:PGPASSWORD = $conn.Password
try {
    & $pgDump --host $conn.Host --port $conn.Port --username $conn.Username --dbname $conn.Database `
              --format=custom --compress=6 --no-owner --no-privileges --file $file
    if ($LASTEXITCODE -ne 0) { throw "pg_dump exited with $LASTEXITCODE. The backup was NOT taken." }
} finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

$size = (Get-Item $file).Length
if ($size -lt 1024) { throw "The backup file is only $size bytes, which is not a database. Something went wrong." }
Write-Host ("Backup written: {0:N1} MB" -f ($size / 1MB)) -ForegroundColor Green

# Prune. Only files this script named are touched.
$cutoff = (Get-Date).AddDays(-$KeepDays)
Get-ChildItem $BackupPath -Filter "$($conn.Database)-*.dump" |
    Where-Object { $_.LastWriteTime -lt $cutoff } |
    ForEach-Object { Write-Host "Removing old backup $($_.Name)"; Remove-Item $_.FullName }

Write-Host "To restore:  .\Restore-Database.ps1 -BackupFile `"$file`" -Confirm"
