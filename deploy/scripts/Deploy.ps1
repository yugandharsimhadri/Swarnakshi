<#
.SYNOPSIS
    Installs or upgrades Swarnakshi on this server. The same script does the first deployment and
    every incremental one after it.

.DESCRIPTION
    Run from inside a package produced by Publish.ps1, as Administrator:

        cd C:\Swarnakshi\packages\2026.09.04-bf42c09
        .\scripts\Deploy.ps1 -DbPassword '<database password>'      # first time
        .\scripts\Deploy.ps1                                        # every time after

    What it does, in order:

        1. Checks the prerequisites, so it fails on the first line rather than half way through.
        2. Backs up SCOPS, so a rollback has something to restore.
        3. Keeps the previous release folder, so a rollback has something to swap back to.
        4. Stops the service.
        5. Copies the new binaries in, preserving appsettings.Production.json and uploaded files.
        6. Applies migrations as an explicit step (--migrate). A failure here stops the deployment
           while the service is still down, so nothing ever serves a schema it does not understand.
        7. Starts the service and waits for /health to answer.
        8. If anything after step 4 fails, puts the previous release back and starts it.

    The database password is only needed on the first run, when the settings file is created.
    After that the existing settings file is left exactly as it is.

.EXAMPLE
    .\scripts\Deploy.ps1 -DbPassword '<database password>'
.EXAMPLE
    .\scripts\Deploy.ps1 -Port 8080
#>
[CmdletBinding()]
param(
    [string] $AppRoot     = 'C:\Swarnakshi',
    [string] $ServiceName = 'Swarnakshi',
    [int]    $Port        = 8080,
    [string] $Server      = '.\SQLEXPRESS',
    [string] $Database    = 'SCOPS',
    [string] $DbPassword,                       # first deployment only
    [string] $PlatformAdminPassword,            # optional; omit to keep the built-in default
    [switch] $SkipBackup,
    [int]    $HealthTimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
$package  = Resolve-Path (Join-Path $PSScriptRoot '..')
$appDir   = Join-Path $AppRoot 'app'
$prevDir  = Join-Path $AppRoot 'previous'
$dataDir  = Join-Path $AppRoot 'data'
$settings = Join-Path $appDir 'appsettings.Production.json'
$exe      = Join-Path $appDir 'Swarnakshi.Api.exe'
$health   = "http://localhost:$Port/health"

function Step($n, $text) { Write-Host "`n[$n] $text" -ForegroundColor Cyan }

# ---------------------------------------------------------------- 1. prerequisites
Step 1 'Checking prerequisites'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "Run this from an elevated PowerShell. It installs a service and writes under $AppRoot."
}
if (-not (Test-Path (Join-Path $package 'app\Swarnakshi.Api.dll'))) {
    throw "This does not look like a Publish.ps1 package - app\Swarnakshi.Api.dll is missing."
}
if (-not (Test-Path (Join-Path $package 'app\wwwroot\index.html'))) {
    throw "The package has no built UI in app\wwwroot. Re-run Publish.ps1."
}

$sqlSvc = Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
if (-not $sqlSvc) { throw "SQL Server Express is not installed on this machine." }
if ($sqlSvc.Status -ne 'Running') { throw "SQL Server Express is installed but not running." }

$firstRun = -not (Test-Path $settings)
if ($firstRun -and -not $DbPassword) {
    throw ("First deployment: pass -DbPassword so the settings file can be created. " +
           "Create the database first with sql\01-create-database.sql.")
}

$build = 'unknown'
if (Test-Path (Join-Path $package 'build.json')) {
    $build = (Get-Content (Join-Path $package 'build.json') -Raw | ConvertFrom-Json).Version
}
$mode = if ($firstRun) { 'first deployment' } else { 'upgrade' }
Write-Host "    Deploying $build to $appDir  ($mode)"

# ---------------------------------------------------------------- 2. backup
if (-not $SkipBackup -and -not $firstRun) {
    Step 2 'Backing up the database'
    & (Join-Path $PSScriptRoot 'Backup-Database.ps1') -Server $Server -Database $Database `
        -BackupPath (Join-Path $AppRoot 'backups') -Label "pre-$build" | Out-Host
} else {
    Step 2 'Skipping the backup (first deployment, or -SkipBackup)'
}

$rollbackNeeded = $false
try {
    # ------------------------------------------------------------ 3. stop
    Step 3 'Stopping the service'
    $svc = Get-Service $ServiceName -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -ne 'Stopped') {
        Stop-Service $ServiceName -Force
        (Get-Service $ServiceName).WaitForStatus('Stopped', '00:00:30')
        # The file lock outlives the "Stopped" status by a moment; copying too early fails.
        Start-Sleep -Seconds 2
    }

    # ------------------------------------------------------------ 4. keep the old release
    Step 4 'Keeping the current release for rollback'
    if (Test-Path $appDir) {
        if (Test-Path $prevDir) { Remove-Item $prevDir -Recurse -Force }
        Copy-Item $appDir $prevDir -Recurse
        $rollbackNeeded = $true
        Write-Host "    Previous release copied to $prevDir"
    }

    # ------------------------------------------------------------ 5. the new binaries
    Step 5 'Copying the new release'
    New-Item -ItemType Directory -Force -Path $appDir, $dataDir, (Join-Path $dataDir 'uploads') | Out-Null

    # Preserve the settings file across the copy: it holds the secrets and is not in the package.
    $keptSettings = $null
    if (Test-Path $settings) { $keptSettings = Get-Content $settings -Raw }

    # wwwroot is emptied rather than merged, because a stale hashed asset left behind would be
    # served to a browser that has just been handed a new index.html.
    Get-ChildItem $appDir -Exclude 'appsettings.Production.json' | Remove-Item -Recurse -Force
    Copy-Item (Join-Path $package 'app\*') $appDir -Recurse -Force

    if ($keptSettings) { $keptSettings | Out-File $settings -Encoding utf8 -NoNewline }

    if ($firstRun) {
        Step '5b' 'Writing appsettings.Production.json'
        $settingsArgs = @{
            DbPassword = $DbPassword; Server = $Server; Database = $Database
            AppRoot = $AppRoot; ListenUrl = "http://0.0.0.0:$Port"
        }
        if ($PlatformAdminPassword) { $settingsArgs.PlatformAdminPassword = $PlatformAdminPassword }
        & (Join-Path $PSScriptRoot 'New-ProductionSettings.ps1') @settingsArgs | Out-Host
    }

    # ------------------------------------------------------------ 6. schema
    Step 6 'Applying database migrations'
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    & $exe --migrate
    if ($LASTEXITCODE -ne 0) { throw "Migration failed (exit $LASTEXITCODE). The schema was not changed." }
    Write-Host "    Schema is up to date." -ForegroundColor Green

    # ------------------------------------------------------------ 7. run it
    Step 7 'Starting the service'
    if (-not (Get-Service $ServiceName -ErrorAction SilentlyContinue)) {
        New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName 'Swarnakshi' `
            -Description 'Swarnakshi construction management - API and web UI.' `
            -StartupType Automatic | Out-Null
        # Restart twice on a crash, then leave it alone so a genuine failure stays visible.
        & sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/20000/"" | Out-Null
        & sc.exe failureflag $ServiceName 1 | Out-Null
        Write-Host "    Service '$ServiceName' created (starts automatically at boot)."
    }
    # The service runs as LocalSystem, which is one of the two principals
    # New-ProductionSettings.ps1 leaves able to read the settings file.
    Start-Service $ServiceName

    # ------------------------------------------------------------ 8. prove it is up
    Step 8 "Waiting for $health"
    $deadline = (Get-Date).AddSeconds($HealthTimeoutSeconds)
    $ok = $false
    while ((Get-Date) -lt $deadline) {
        try {
            if ((Invoke-RestMethod $health -TimeoutSec 5).status -eq 'ok') { $ok = $true; break }
        } catch { Start-Sleep -Seconds 2 }
    }
    if (-not $ok) { throw "The service did not become healthy within $HealthTimeoutSeconds seconds." }

    Write-Host "`nDeployed $build successfully." -ForegroundColor Green
    Write-Host "  Service : $ServiceName ($((Get-Service $ServiceName).Status))"
    Write-Host "  URL     : http://$($env:COMPUTERNAME):$Port/"
    Write-Host "  Health  : $health"
    Write-Host "  Rollback: .\scripts\Rollback.ps1"
}
catch {
    Write-Host "`nDEPLOYMENT FAILED: $($_.Exception.Message)" -ForegroundColor Red
    if ($rollbackNeeded -and (Test-Path $prevDir)) {
        Write-Host "Putting the previous release back..." -ForegroundColor Yellow
        Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Get-ChildItem $appDir -Exclude 'appsettings.Production.json' | Remove-Item -Recurse -Force
        Copy-Item (Join-Path $prevDir '*') $appDir -Recurse -Force
        Start-Service $ServiceName -ErrorAction SilentlyContinue
        Write-Host "Previous release restored and started." -ForegroundColor Yellow
        Write-Host "The database was NOT rolled back. If the migration applied before the failure," -ForegroundColor Yellow
        Write-Host "restore the backup from step 2 - see docs/06-deployment.md, 'Rolling back'." -ForegroundColor Yellow
    }
    exit 1
}
