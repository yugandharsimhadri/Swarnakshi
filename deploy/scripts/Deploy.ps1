<#
.SYNOPSIS
    Installs or upgrades Swarnakshi on this server. The same script does the first deployment and
    every incremental one after it.

.DESCRIPTION
    All settings - server name, database, login, password, port, signing key - live in ONE file:

        C:\Swarnakshi\app\appsettings.Production.json

    Edit that file to change any of them, then restart the service. This script reads it, never
    overwrites it, and preserves it across every upgrade, so your edits survive deployments.

    FIRST DEPLOYMENT
        1. Create the database and its login yourself, and grant the login rights on it.
        2. Copy appsettings.Production.template.json (beside this package, in the root) to
           C:\Swarnakshi\app\appsettings.Production.json and edit the connection string,
           the Jwt:Key and the port.
        3. Run, from an elevated PowerShell:

               .\scripts\Deploy.ps1

    EVERY DEPLOYMENT AFTER THAT
        Copy the new package over and run the same command. Nothing else.

               .\scripts\Deploy.ps1

    -InitSettings writes the settings file for you from the template instead of you copying it,
    generating a signing key. Use it once, on the first deployment, if you would rather not hand-edit.

.EXAMPLE
    .\scripts\Deploy.ps1
.EXAMPLE
    # first run, letting the script create the settings file
    .\scripts\Deploy.ps1 -InitSettings -ConnectionString 'Server=.\SQLEXPRESS;Database=SCOPS;User ID=SivayaanHMS;Password=...;TrustServerCertificate=True'
.EXAMPLE
    .\scripts\Deploy.ps1 -SkipBackup
#>
[CmdletBinding()]
param(
    [string] $AppRoot     = 'C:\Swarnakshi',
    [string] $ServiceName = 'Swarnakshi',

    # Only used with -InitSettings, when the settings file is being created for the first time.
    [switch] $InitSettings,
    [string] $ConnectionString,
    [int]    $Port = 6061,
    [string[]] $CorsOrigins = @(),

    [switch] $SkipBackup,
    [switch] $SkipDbCheck,
    [int]    $HealthTimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
$package  = Resolve-Path (Join-Path $PSScriptRoot '..')
$appDir   = Join-Path $AppRoot 'app'
$prevDir  = Join-Path $AppRoot 'previous'
$dataDir  = Join-Path $AppRoot 'data'
$settings = Join-Path $appDir 'appsettings.Production.json'
$exe      = Join-Path $appDir 'Swarnakshi.Api.exe'

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

# ---------------------------------------------------------------- 2. the settings file
Step 2 'Reading the settings file'

if (-not (Test-Path $settings)) {
    if (-not $InitSettings) {
        throw @"
No settings file at
    $settings

Create it before deploying - it holds the connection string, and this script will not invent one.
Either:

  a) copy the template and edit it:
         New-Item -ItemType Directory -Force -Path '$appDir'
         Copy-Item '$package\appsettings.Production.template.json' '$settings'
         notepad '$settings'

  b) or let this script write it:
         .\scripts\Deploy.ps1 -InitSettings -ConnectionString '<your connection string>'
"@
    }
    if (-not $ConnectionString) {
        throw "-InitSettings needs -ConnectionString '<your connection string>'."
    }
    # localhost, not 0.0.0.0: cloudflared runs on this machine and connects out, so nothing has to
    # reach the port from the network. Edit Urls in the settings file if it ever should.
    & (Join-Path $PSScriptRoot 'New-ProductionSettings.ps1') `
        -ConnectionString $ConnectionString -AppRoot $AppRoot -ListenUrl "http://localhost:$Port" `
        -CorsOrigins $CorsOrigins | Out-Host
}

# From here the settings file is authoritative. Nothing below writes to it.
try {
    $cfg = Get-Content $settings -Raw | ConvertFrom-Json
} catch {
    throw "$settings is not valid JSON: $($_.Exception.Message)"
}

$conn = $cfg.ConnectionStrings.Default
if ([string]::IsNullOrWhiteSpace($conn) -or $conn -match 'CHANGE_ME') {
    throw "Set ConnectionStrings:Default in $settings before deploying."
}
if ([string]::IsNullOrWhiteSpace($cfg.Jwt.Key) -or $cfg.Jwt.Key -match 'CHANGE_ME') {
    throw "Set Jwt:Key in $settings (at least 32 characters). The app refuses to start without it."
}
if ($cfg.Jwt.Key.Length -lt 32) {
    throw "Jwt:Key in $settings is only $($cfg.Jwt.Key.Length) characters. It must be at least 32."
}

# Pull the pieces back out of the connection string for the checks below and for the log line.
function Get-ConnPart([string]$c, [string[]]$keys) {
    foreach ($k in $keys) {
        if ($c -match ("(?i)(^|;)\s*" + [regex]::Escape($k) + "\s*=\s*([^;]*)")) { return $Matches[2].Trim() }
    }
    $null
}
$dbServer = Get-ConnPart $conn @('Server', 'Data Source', 'Addr', 'Address')
$dbName   = Get-ConnPart $conn @('Database', 'Initial Catalog')
$dbUser   = Get-ConnPart $conn @('User ID', 'UserId', 'Uid')
$dbPass   = Get-ConnPart $conn @('Password', 'Pwd')
$trusted  = (Get-ConnPart $conn @('Trusted_Connection', 'Integrated Security')) -match '(?i)true|sspi|yes'

$listenUrl = if ($cfg.Urls) { $cfg.Urls } else { "http://localhost:$Port" }
if ($listenUrl -match ':(\d+)\s*$') { $Port = [int]$Matches[1] }
$health = "http://localhost:$Port/health"

Write-Host "    Database : $dbName on $dbServer as $(if ($trusted) { 'the service account' } else { $dbUser })"
Write-Host "    Listening: $listenUrl"

# ---------------------------------------------------------------- 3. is the database reachable
if ($SkipDbCheck) {
    Step 3 'Skipping the database check (-SkipDbCheck)'
} else {
    Step 3 "Checking [$dbName] on [$dbServer]"
    # Prove the credentials in the settings file actually work, rather than assuming a service
    # named MSSQL$SQLEXPRESS exists locally - the instance may be named differently or live on
    # another host, and a running service says nothing about whether the login can get in.
    $args = @('-S', $dbServer, '-d', $dbName, '-C', '-b', '-h-1', '-W', '-l', '10',
              '-Q', "SET NOCOUNT ON; SELECT 'reachable';")
    if ($trusted) { $args = @('-E') + $args } else { $args = @('-U', $dbUser, '-P', $dbPass) + $args }

    $probe = & sqlcmd @args 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw @"
Cannot reach [$dbName] on [$dbServer] with the credentials in the settings file.

    $probe

Fix the connection string in
    $settings
or create the database and grant the login. It needs db_datareader, db_datawriter, EXECUTE, and
enough DDL rights for EF Core migrations - CREATE TABLE and ALTER on schema dbo. Then re-run.
(-SkipDbCheck bypasses this check, but step 6 will fail anyway if the login cannot connect.)
"@
    }
    Write-Host "    Reachable." -ForegroundColor Green
}

$build = 'unknown'
if (Test-Path (Join-Path $package 'build.json')) {
    $build = (Get-Content (Join-Path $package 'build.json') -Raw | ConvertFrom-Json).Version
}
$installed = Test-Path $exe
Write-Host "`n    Deploying $build to $appDir  ($(if ($installed) { 'upgrade' } else { 'first deployment' }))"

# ---------------------------------------------------------------- 4. backup
if (-not $SkipBackup -and $installed) {
    Step 4 'Backing up the database'
    try {
        & (Join-Path $PSScriptRoot 'Backup-Database.ps1') -Server $dbServer -Database $dbName `
            -BackupPath (Join-Path $AppRoot 'backups') -Label "pre-$build" | Out-Host
    } catch {
        # BACKUP DATABASE needs db_backupoperator or sysadmin, and the account running a deployment
        # does not always have it. Stopping here is deliberate: upgrading the schema with no restore
        # point behind it should be a decision, not an accident.
        throw ("The backup failed: $($_.Exception.Message)`n" +
               "Grant the deploying account db_backupoperator on [$dbName], take a backup yourself " +
               "first, or re-run with -SkipBackup to proceed deliberately without one.")
    }
} elseif (-not $installed) {
    Step 4 'Skipping the backup (first deployment - there is nothing to lose yet)'
} else {
    Step 4 'Skipping the backup (-SkipBackup)'
    Write-Warning 'Deploying without a restore point. If the migration goes wrong there is nothing to go back to.'
}

$rollbackNeeded = $false
try {
    # ------------------------------------------------------------ 5. stop
    Step 5 'Stopping the service'
    $svc = Get-Service $ServiceName -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -ne 'Stopped') {
        Stop-Service $ServiceName -Force
        (Get-Service $ServiceName).WaitForStatus('Stopped', '00:00:30')
        # The file lock outlives the "Stopped" status by a moment; copying too early fails.
        Start-Sleep -Seconds 2
    }

    # ------------------------------------------------------------ 6. keep the old release
    Step 6 'Keeping the current release for rollback'
    if ($installed) {
        if (Test-Path $prevDir) { Remove-Item $prevDir -Recurse -Force }
        Copy-Item $appDir $prevDir -Recurse
        $rollbackNeeded = $true
        Write-Host "    Previous release copied to $prevDir"
    }

    # ------------------------------------------------------------ 7. the new binaries
    Step 7 'Copying the new release'
    New-Item -ItemType Directory -Force -Path $appDir, $dataDir, (Join-Path $dataDir 'uploads') | Out-Null

    # The settings file is yours, not the package's. Hold it aside and put it back untouched.
    $keptSettings = Get-Content $settings -Raw

    # wwwroot is emptied rather than merged: a stale hashed asset left behind would be served to a
    # browser that has just been handed a new index.html.
    Get-ChildItem $appDir -Exclude 'appsettings.Production.json' | Remove-Item -Recurse -Force
    Copy-Item (Join-Path $package 'app\*') $appDir -Recurse -Force
    $keptSettings | Out-File $settings -Encoding utf8 -NoNewline

    # ------------------------------------------------------------ 8. schema
    Step 8 'Applying database migrations'
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    & $exe --migrate
    if ($LASTEXITCODE -ne 0) { throw "Migration failed (exit $LASTEXITCODE). The schema was not changed." }
    Write-Host "    Schema is up to date." -ForegroundColor Green

    # ------------------------------------------------------------ 9. run it
    Step 9 'Starting the service'
    if (-not (Get-Service $ServiceName -ErrorAction SilentlyContinue)) {
        New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName 'Swarnakshi' `
            -Description 'Swarnakshi construction management - API and web UI.' `
            -StartupType Automatic | Out-Null
        # Restart twice on a crash, then leave it alone so a genuine failure stays visible.
        & sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/20000/"" | Out-Null
        & sc.exe failureflag $ServiceName 1 | Out-Null
        Write-Host "    Service '$ServiceName' created (starts automatically at boot)."
    }
    Start-Service $ServiceName

    # ------------------------------------------------------------ 10. prove it is up
    Step 10 "Waiting for $health"
    $deadline = (Get-Date).AddSeconds($HealthTimeoutSeconds)
    $ok = $false
    while ((Get-Date) -lt $deadline) {
        try {
            if ((Invoke-RestMethod $health -TimeoutSec 5).status -eq 'ok') { $ok = $true; break }
        } catch { Start-Sleep -Seconds 2 }
    }
    if (-not $ok) { throw "The service did not become healthy within $HealthTimeoutSeconds seconds." }

    Write-Host "`nDeployed $build successfully." -ForegroundColor Green
    Write-Host "  Service  : $ServiceName ($((Get-Service $ServiceName).Status))"
    Write-Host "  URL      : http://$($env:COMPUTERNAME):$Port/"
    Write-Host "  Health   : $health"
    Write-Host "  Settings : $settings   (edit, then Restart-Service $ServiceName)"
    Write-Host "  Rollback : .\scripts\Rollback.ps1"
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
        Write-Host "restore the backup from step 4 - see docs/06-deployment.md, 'Rolling back'." -ForegroundColor Yellow
    }
    exit 1
}
