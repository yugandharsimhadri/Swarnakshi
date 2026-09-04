<#
.SYNOPSIS
    Puts the previous release back.

.DESCRIPTION
    Deploy.ps1 rolls back on its own when a deployment fails. This script is for the other case:
    the deployment succeeded, and something is wrong that only real use revealed.

    It swaps the binaries only. If the release being undone changed the schema, the new columns and
    tables stay - which is usually fine, because migrations here are additive and the old binaries
    ignore what they do not know about. When it is not fine, restore the backup:

        .\Restore-Database.ps1 -BackupFile C:\Swarnakshi\backups\SCOPS-<stamp>-pre-<version>.bak

    That loses everything entered since the backup was taken, so it is the second choice, not the
    first. Read docs/06-deployment.md before running it.
#>
[CmdletBinding()]
param(
    [string] $AppRoot     = 'C:\Swarnakshi',
    [string] $ServiceName = 'Swarnakshi',
    [int]    $Port        = 6061
)

$ErrorActionPreference = 'Stop'
$appDir  = Join-Path $AppRoot 'app'
$prevDir = Join-Path $AppRoot 'previous'

if (-not (Test-Path $prevDir)) { throw "No previous release at $prevDir - nothing to roll back to." }

Write-Host "Rolling back to the release in $prevDir" -ForegroundColor Yellow
Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# The settings file is not part of a release and must survive the swap.
Get-ChildItem $appDir -Exclude 'appsettings.Production.json' | Remove-Item -Recurse -Force
Copy-Item (Join-Path $prevDir '*') $appDir -Recurse -Force

Start-Service $ServiceName
Start-Sleep -Seconds 3
try {
    $s = (Invoke-RestMethod "http://localhost:$Port/health" -TimeoutSec 10).status
    Write-Host "Rolled back. Health: $s" -ForegroundColor Green
} catch {
    Write-Host "Rolled back, but /health did not answer. Check the Application event log." -ForegroundColor Red
}
