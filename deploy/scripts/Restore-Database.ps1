<#
.SYNOPSIS
    Restores SCOPS from a backup file.

.DESCRIPTION
    This overwrites the live database. Everything entered since the backup was taken is lost.
    It is the last option, not the first - read docs/06-deployment.md, "Rolling back", before
    running it, and stop the application service first so nothing is writing during the restore.

    The script refuses to run unless -Confirm is given, and it takes a safety backup of the
    current state first, so a mistaken restore is itself recoverable.

.EXAMPLE
    Stop-Service Swarnakshi
    .\Restore-Database.ps1 -BackupFile C:\Swarnakshi\backups\SCOPS-20260904-081500-pre-2026.09.04.bak -Confirm
    Start-Service Swarnakshi
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $BackupFile,
    [string] $Server   = '.\SQLEXPRESS',
    [string] $Database = 'SCOPS',
    [string] $BackupPath = 'C:\Swarnakshi\backups',
    [switch] $Confirm
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $BackupFile)) { throw "No such backup file: $BackupFile" }
if (-not $Confirm) {
    throw ("This replaces the whole of [$Database] with the contents of $BackupFile, and everything " +
           "entered since that backup was taken is lost. Re-run with -Confirm if that is what you want.")
}

$svc = Get-Service 'Swarnakshi' -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq 'Running') {
    throw "Stop the Swarnakshi service first: Stop-Service Swarnakshi"
}

Write-Host "Verifying $BackupFile ..."
& sqlcmd -S $Server -E -C -b -Q "RESTORE VERIFYONLY FROM DISK = N'$BackupFile' WITH CHECKSUM;"
if ($LASTEXITCODE -ne 0) { throw "The backup file did not verify. Nothing was changed." }

# A restore is undoable only if the thing it overwrites was itself captured first.
Write-Host "Taking a safety backup of the current $Database ..."
& (Join-Path $PSScriptRoot 'Backup-Database.ps1') -Server $Server -Database $Database `
    -BackupPath $BackupPath -Label 'before-restore' | Out-Host

$sql = @"
ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$Database] FROM DISK = N'$BackupFile' WITH REPLACE, RECOVERY, STATS = 25;
ALTER DATABASE [$Database] SET MULTI_USER;
"@

Write-Host "Restoring ..." -ForegroundColor Yellow
& sqlcmd -S $Server -E -C -b -Q $sql
if ($LASTEXITCODE -ne 0) {
    # SINGLE_USER can be left behind by a failed restore, which locks everyone else out.
    & sqlcmd -S $Server -E -C -Q "ALTER DATABASE [$Database] SET MULTI_USER;" 2>&1 | Out-Null
    throw "The restore failed. The safety backup above still holds the state from before it started."
}

Write-Host "Restored $Database from $BackupFile" -ForegroundColor Green
Write-Host "Start the service when you are ready:  Start-Service Swarnakshi"
