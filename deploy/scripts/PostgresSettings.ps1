<#
    Shared by the database scripts: where the application's settings file is, how to read the
    connection string out of it, and where the PostgreSQL command-line tools are.

    Dot-source it:   . (Join-Path $PSScriptRoot 'PostgresSettings.ps1')

    The connection string is read from appsettings.Production.json rather than passed in, so a
    backup, a restore, a diagnosis and the application itself all agree on which database they
    mean. There is exactly one place to change it.
#>

function Read-PostgresConnection {
    param([Parameter(Mandatory)] [string] $AppRoot)

    $candidates = @(
        (Join-Path $AppRoot 'app\appsettings.Production.json'),
        (Join-Path $AppRoot 'appsettings.Production.json')
    )
    $file = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $file) {
        throw "No appsettings.Production.json under $AppRoot (looked in app\ and the root). Pass -AppRoot <where the app is installed>."
    }

    $settings = Get-Content $file -Raw | ConvertFrom-Json
    $cs = $settings.ConnectionStrings.Default
    if ([string]::IsNullOrWhiteSpace($cs)) { throw "$file has no ConnectionStrings:Default." }

    # Npgsql keys: Host, Port, Database, Username, Password. Case-insensitive; a few aliases.
    $parts = @{}
    foreach ($pair in ($cs -split ';')) {
        if ($pair -notmatch '=') { continue }
        $k, $v = $pair -split '=', 2
        $parts[$k.Trim().ToLowerInvariant()] = $v.Trim()
    }
    $pick = { param($names) foreach ($n in $names) { if ($parts.ContainsKey($n)) { return $parts[$n] } } return $null }

    $result = [pscustomobject]@{
        File     = $file
        Host     = & $pick @('host', 'server')
        Port     = & $pick @('port')
        Database = & $pick @('database', 'db')
        Username = & $pick @('username', 'user id', 'userid', 'user')
        Password = & $pick @('password', 'pwd')
    }
    if (-not $result.Port) { $result.Port = '5432' }
    foreach ($required in 'Host', 'Database', 'Username', 'Password') {
        if (-not $result.$required) { throw "ConnectionStrings:Default in $file has no $required." }
    }
    return $result
}

function Find-PgTool {
    param([Parameter(Mandatory)] [string] $Name, [string] $PgBin = '')

    if ($PgBin) {
        $exe = Join-Path $PgBin "$Name.exe"
        if (Test-Path $exe) { return $exe }
        throw "$exe not found."
    }

    $onPath = Get-Command $Name -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    # The installer does not put the bin folder on PATH. Newest installed major version wins.
    $root = 'C:\Program Files\PostgreSQL'
    if (Test-Path $root) {
        $found = Get-ChildItem $root -Directory |
            Where-Object { $_.Name -match '^\d+$' } |
            Sort-Object { [int]$_.Name } -Descending |
            ForEach-Object { Join-Path $_.FullName "bin\$Name.exe" } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
        if ($found) { return $found }
    }

    throw "$Name.exe not found on PATH or under $root. Install the PostgreSQL client tools, or pass -PgBin <folder>."
}
