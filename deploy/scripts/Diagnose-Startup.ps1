<#
.SYNOPSIS
    Works out why the API will not start, in one run, on the server.

.DESCRIPTION
    HTTP 500.30 means the process died before it could serve anything. IIS shows that page and
    nothing else, so the reason is somewhere the browser cannot reach: the log file, the Windows
    event log, or an exception thrown before logging was even configured.

    This looks in all of them, in the order the startup path actually fails:

        1. Is anything running at all
        2. appsettings.Production.json - present, parseable, and carrying a usable Jwt:Key
        3. The log directory - the first thing Program.cs touches, and a failure here logs nothing
        4. The newest log file, errors first
        5. The ASP.NET Core Module's stdout capture in the Windows event log
        6. The database - can the app's own connection string open it, and is the schema current
        7. Optionally, the real exception: runs the published exe and shows what it says

    Read-only apart from -RunMigrate, which applies pending migrations exactly as a deployment does.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Diagnose-Startup.ps1

.EXAMPLE
    # Also run the app to get the actual exception text, and apply any pending schema change:
    powershell -ExecutionPolicy Bypass -File Diagnose-Startup.ps1 -RunMigrate
#>
[CmdletBinding()]
param(
    [string] $AppRoot = '',
    [switch] $RunMigrate
)

$ErrorActionPreference = 'Continue'
$problems = New-Object System.Collections.Generic.List[string]

function Section($n) { Write-Host "`n===== $n " -ForegroundColor Cyan }
function Bad($m) { Write-Host "  FAIL  $m" -ForegroundColor Red; $problems.Add($m) }
function Ok($m)  { Write-Host "  ok    $m" -ForegroundColor Green }
function Info($m){ Write-Host "        $m" -ForegroundColor Gray }

# ---- 0. find the app, rather than assuming where it is -----------------------
# The two supported layouts put the binaries in different places - Deploy.ps1 uses <root>\app\,
# and an IIS site created by hand often points straight at the folder. Guessing wrong makes every
# check below report a missing file, which reads like a broken install and is not.
function Resolve-AppFolder([string] $hint) {
    $candidates = @()
    if ($hint) { $candidates += (Join-Path $hint 'app'), $hint }

    # Ask IIS where its sites actually live. This is how you find an install nobody documented.
    try {
        Import-Module WebAdministration -ErrorAction Stop
        foreach ($site in (Get-Website -ErrorAction SilentlyContinue)) {
            $p = [Environment]::ExpandEnvironmentVariables($site.physicalPath)
            if ($p) { $candidates += $p, (Join-Path $p 'app') }
        }
    } catch { }

    $candidates += 'C:\Swarnakshi\app', 'C:\Swarnakshi'

    foreach ($c in ($candidates | Where-Object { $_ } | Select-Object -Unique)) {
        if (Test-Path (Join-Path $c 'Swarnakshi.Api.dll')) { return $c }
    }
    return $null
}

$app = Resolve-AppFolder $AppRoot
if (-not $app) {
    $app = if ($AppRoot) { Join-Path $AppRoot 'app' } else { 'C:\Swarnakshi\app' }
    Bad "Could not find Swarnakshi.Api.dll in any IIS site or the usual locations. Pass -AppRoot <the site's physical path>."
}
if (-not $AppRoot) { $AppRoot = Split-Path $app -Parent }
$settingsPath = Join-Path $app 'appsettings.Production.json'

Write-Host "Swarnakshi startup diagnosis - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor White
Info "binaries: $app"

# ---- 1. is anything running --------------------------------------------------
Section '1. Process'
$proc = Get-Process Swarnakshi.Api -ErrorAction SilentlyContinue
if ($proc) { Ok "Swarnakshi.Api is running (pid $($proc.Id -join ', '))" }
else { Info "Swarnakshi.Api is not running as a standalone process (normal under IIS)." }

$svc = Get-Service Swarnakshi -ErrorAction SilentlyContinue
if ($svc) { Info "Windows service 'Swarnakshi': $($svc.Status)" }

try {
    Import-Module WebAdministration -ErrorAction Stop
    $pool = Get-Item 'IIS:\AppPools\Swarnakshi' -ErrorAction SilentlyContinue
    if ($pool) {
        Info "IIS app pool 'Swarnakshi': $($pool.state)"
        # A pool that has stopped itself is rapid-fail protection: five crashes in five minutes and
        # IIS stops trying. Every request then returns 503, not 500.30 - worth telling apart.
        if ($pool.state -ne 'Started') { Bad "The app pool is $($pool.state). Rapid-fail protection stops a pool that crashed repeatedly; start it after fixing the cause." }
    }
} catch { Info "WebAdministration not available - skipping IIS checks." }

# ---- 2. the settings file ----------------------------------------------------
Section '2. appsettings.Production.json'
if (-not (Test-Path $settingsPath)) {
    Bad "$settingsPath is missing. A deployment that replaced the app folder without putting it back is the usual reason; the app then throws on Jwt:Key and never starts."
    $settings = $null
} else {
    $raw = Get-Content $settingsPath -Raw
    Ok "present ($((Get-Item $settingsPath).Length) bytes, modified $((Get-Item $settingsPath).LastWriteTime))"
    try {
        $settings = $raw | ConvertFrom-Json
        Ok "parses as JSON"
    } catch {
        Bad "It is not valid JSON: $($_.Exception.Message)  The host cannot read its configuration and dies before logging anything."
        $settings = $null
    }
}

if ($settings) {
    $key = $settings.Jwt.Key
    if ([string]::IsNullOrWhiteSpace($key)) { Bad "Jwt:Key is missing. Outside Development the app throws 'Jwt:Key must be set (>=32 chars)' and exits." }
    elseif ($key.Length -lt 32)             { Bad "Jwt:Key is $($key.Length) characters; it must be at least 32." }
    else                                    { Ok "Jwt:Key present ($($key.Length) chars)" }

    $cs = $settings.ConnectionStrings.Default
    if ([string]::IsNullOrWhiteSpace($cs)) { Bad "ConnectionStrings:Default is missing." }
    else { Ok "connection string present"; Info ($cs -replace 'Password=[^;]*', 'Password=***') }

    $origins = @($settings.Cors.Origins)
    if ($origins.Count -eq 0) { Info "Cors:Origins is empty - fine only if this service also serves the UI." }
    else { Info "Cors:Origins: $($origins -join ', ')" }

    # Readability by the account the app runs as. An unreadable settings file fails the same way as
    # a missing one, and looks fine in Explorer.
    try { [void](Get-Content $settingsPath -TotalCount 1); Ok "readable by the current user" }
    catch { Bad "Cannot read the settings file: $($_.Exception.Message)" }
}

# ---- 3. the log directory ----------------------------------------------------
Section '3. Log directory'
$logDir = $null
if ($settings -and $settings.Logging -and $settings.Logging.Directory) { $logDir = $settings.Logging.Directory }
# Program.cs derives it from where the binaries are: the PARENT of the app folder, plus \logs. So
# an app at F:\sivayaan\copsapi logs to F:\sivayaan\logs, not beside itself.
if (-not $logDir) { $logDir = Join-Path (Split-Path $app -Parent) 'logs' }
Info "expected at: $logDir"

if (-not (Test-Path $logDir)) {
    Bad "The log directory does not exist. Program.cs creates it before anything else, so a failure here means the process dies with nothing written anywhere."
} else {
    Ok "exists"
    try {
        $probe = Join-Path $logDir ".write-probe"
        Set-Content -Path $probe -Value 'x' -ErrorAction Stop
        Remove-Item $probe -ErrorAction SilentlyContinue
        Ok "writable by the current user"
    } catch {
        Bad "Not writable: $($_.Exception.Message)  Grant the app pool identity Modify: icacls `"$logDir`" /grant `"IIS AppPool\Swarnakshi:(OI)(CI)M`""
    }
}

# ---- 4. the newest log -------------------------------------------------------
Section '4. Newest log file'
$log = if (Test-Path $logDir) { Get-ChildItem $logDir -Filter 'swarnakshi-*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1 }
if (-not $log) {
    Bad "No log file. Either the app has never started far enough to open one, or it cannot write to the directory. Skip to section 7."
} else {
    Info "$($log.FullName)  ($('{0:N0}' -f $log.Length) bytes, last written $($log.LastWriteTime))"
    if ($log.Length -eq 0) { Bad "The log is empty - the process died before Serilog wrote its first line." }

    $errors = Select-String -Path $log.FullName -Pattern '\[(ERR|FTL)\]' -ErrorAction SilentlyContinue | Select-Object -Last 25
    if ($errors) {
        Write-Host "`n  --- errors and fatals (last 25) ---" -ForegroundColor Yellow
        $errors | ForEach-Object { Write-Host "  $($_.Line)" }
    } else {
        Info "no [ERR] or [FTL] lines"
    }
    Write-Host "`n  --- last 30 lines ---" -ForegroundColor Yellow
    Get-Content $log.FullName -Tail 30 | ForEach-Object { Write-Host "  $_" }
}

# ---- 5. what IIS captured ----------------------------------------------------
Section '5. Windows event log (ASP.NET Core Module)'
# When the process dies before Serilog exists, this is the only place the exception is recorded.
$events = Get-WinEvent -FilterHashtable @{
    LogName = 'Application'; StartTime = (Get-Date).AddDays(-2)
} -ErrorAction SilentlyContinue |
    Where-Object { $_.ProviderName -like '*AspNetCore*' -or $_.Message -like '*Swarnakshi*' } |
    Select-Object -First 5

if (-not $events) { Info "nothing in the last two days" }
else {
    foreach ($e in $events) {
        Write-Host "`n  [$($e.TimeCreated)] $($e.ProviderName)" -ForegroundColor Yellow
        $text = $e.Message
        Write-Host "  $($text.Substring(0, [Math]::Min(1200, $text.Length)))"
    }
}

# ---- 6. the database ---------------------------------------------------------
Section '6. Database'
if (-not $settings -or [string]::IsNullOrWhiteSpace($settings.ConnectionStrings.Default)) {
    Info "skipped - no connection string to test"
} else {
    $cs = $settings.ConnectionStrings.Default
    $parts = @{}
    foreach ($pair in ($cs -split ';')) { if ($pair -match '=') { $k, $v = $pair -split '=', 2; $parts[$k.Trim().ToLowerInvariant()] = $v.Trim() } }
    $dbHost = if ($parts['host']) { $parts['host'] } else { $parts['server'] }
    $dbPort = if ($parts['port']) { $parts['port'] } else { '5432' }
    $dbName = $parts['database']
    $dbUser = if ($parts['username']) { $parts['username'] } else { $parts['user id'] }
    $dbPass = $parts['password']
    Info "server=${dbHost}:$dbPort  database=$dbName  role=$dbUser"

    # Is the engine even running? "Cannot connect" and "the service is stopped" arrive as the same
    # error from the driver, and only one of them is fixed by starting a service.
    $pgSvc = Get-Service -Name 'postgresql*' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $pgSvc) { Info "no local postgresql* service - the server may be on another machine" }
    else {
        Info "$($pgSvc.Name) is $($pgSvc.Status), start type $($pgSvc.StartType)"
        if ($pgSvc.Status -ne 'Running') { Bad "$($pgSvc.Name) is $($pgSvc.Status). Start it: Start-Service '$($pgSvc.Name)'" }
    }

    $psql = $null
    foreach ($c in @((Get-Command psql -ErrorAction SilentlyContinue).Source),
                    (Get-ChildItem 'C:\Program Files\PostgreSQL' -Directory -ErrorAction SilentlyContinue |
                        Sort-Object { [int]($_.Name -replace '\D','0') } -Descending |
                        ForEach-Object { Join-Path $_.FullName 'bin\psql.exe' })) {
        if ($c -and (Test-Path $c)) { $psql = $c; break }
    }
    if (-not $psql) {
        Info "psql.exe not found - cannot test the connection from here (install the PostgreSQL client tools)"
    } else {
        $env:PGPASSWORD = $dbPass
        try {
            $probe = & $psql --host $dbHost --port $dbPort --username $dbUser --dbname $dbName -w -t -A `
                        -c "SELECT migration_id FROM ""__EFMigrationsHistory"" ORDER BY migration_id;" 2>&1
            $connected = ($LASTEXITCODE -eq 0)
        } finally { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }

        if ($connected) {
            Ok "the application's own connection string opens the database"
            $applied = @($probe | Where-Object { $_ -match '^\d{14}_' })
            Info "migrations applied: $($applied.Count)"
            $applied | ForEach-Object { Info "  $_" }

            # What the binaries sitting in the app folder expect. A mismatch here is a deployment
            # that copied the build but never ran the schema step.
            $expected = @()
            $migDll = Join-Path $app 'Swarnakshi.Infrastructure.dll'
            if (Test-Path $migDll) {
                $bytes = [System.IO.File]::ReadAllBytes($migDll)
                $utf8 = [System.Text.Encoding]::UTF8.GetString($bytes)
                $utf16 = [System.Text.Encoding]::Unicode.GetString($bytes)
                $expected = @([regex]::Matches("$utf8`n$utf16", '\d{14}_[A-Za-z][A-Za-z0-9]*') |
                    ForEach-Object { $_.Value }) | Sort-Object -Unique
            }
            if ($expected.Count -gt 0) {
                $missing = $expected | Where-Object { $applied -notcontains $_ }
                if ($missing) {
                    Bad "The deployed build carries migrations the database has not got: $($missing -join ', ')  Run the upgrade script, or Swarnakshi.Api.exe --migrate."
                } else {
                    Ok "the schema matches the deployed build"
                }
            }
        } else {
            $m = ($probe | Out-String).Trim()
            Bad "Cannot open the database: $m"
            if ($m -match 'does not exist') {
                Info "Create it:  psql -U postgres -h $dbHost -v DbName=`"$dbName`" -v AppRole=`"$dbUser`" -v AppPassword=`"<password>`" -f 01-create-database.sql"
            } elseif ($m -match 'password authentication failed|no pg_hba.conf entry') {
                Info "The role cannot log in from here: wrong password in the settings file, or pg_hba.conf does not allow $dbUser from this address."
            } elseif ($m -match 'refused|could not connect|Connection timed out') {
                Info "Nothing is listening at ${dbHost}:$dbPort. Is the service running, and does listen_addresses in postgresql.conf include this address?"
            }
        }
    }
}

# ---- 7. the actual exception -------------------------------------------------
Section '7. Run it and read the exception'
$exe = Join-Path $app 'Swarnakshi.Api.exe'
if (-not (Test-Path $exe)) {
    Bad "$exe is missing - the app folder does not hold a published build."
} elseif ($RunMigrate) {
    Info "running: $exe --migrate"
    Info "(applies any pending schema change and exits; this is the same step a deployment runs)"
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    Push-Location $app
    try { & $exe --migrate 2>&1 | ForEach-Object { Write-Host "  $_" } } finally { Pop-Location }
    Write-Host "  exit code: $LASTEXITCODE" -ForegroundColor $(if ($LASTEXITCODE -eq 0) { 'Green' } else { 'Red' })
} else {
    Info "Not run. To see the real exception text, re-run this script with -RunMigrate."
}

# ---- verdict -----------------------------------------------------------------
Section 'Summary'
if ($problems.Count -eq 0) {
    Write-Host "  Nothing conclusive found. Re-run with -RunMigrate and send the output of section 7." -ForegroundColor Yellow
} else {
    Write-Host "  $($problems.Count) problem(s) found, most likely cause first:" -ForegroundColor Red
    $i = 1
    foreach ($p in $problems) { Write-Host "   $i. $p"; $i++ }
}
Write-Host ""
