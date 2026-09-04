<#
.SYNOPSIS
    Builds the two things a deployment needs, side by side in deploy\out:

        frontend\   the built React site, to upload to Cloudflare Pages
        app\        the API, to copy into an IIS site
        sql\        the database scripts, to run on the SQL Server
        scripts\    the deployment helpers

.DESCRIPTION
    Two artefacts because they are hosted apart: the UI on Cloudflare's edge, the API on IIS on
    your own machine behind a tunnel. They therefore sit on different origins, and two things follow
    from that and must agree or nothing works:

      * the UI must be built knowing the API's absolute address (-ApiBaseUrl), because a relative
        /api would ask Cloudflare for an endpoint it has never heard of;
      * the API must list the UI's origin in Cors:Origins, because the call is now cross-origin.

    The API still carries the UI inside its own wwwroot as well, so http://localhost:6061 serves a
    complete working site. That is what makes it possible to check the API end to end on the server
    before anything is uploaded, and it costs about 400 KB.

    Run from a clean checkout of the tag being shipped. It refuses to build from a dirty working
    tree unless -AllowDirty is given, so the version stamped into the package names a real commit.

.EXAMPLE
    .\deploy\scripts\Publish.ps1
.EXAMPLE
    # A UI that talks to an API on another hostname:
    .\deploy\scripts\Publish.ps1 -ApiBaseUrl https://copsapi.sivayaantechnologies.com
.EXAMPLE
    .\deploy\scripts\Publish.ps1 -SelfContained    # bundles the .NET runtime; no runtime install needed
#>
[CmdletBinding()]
param(
    [string] $OutputRoot   = (Join-Path $PSScriptRoot '..\out'),
    [string] $Configuration = 'Release',

    # The absolute origin the uploaded UI should call. Leave empty for a UI served by the API
    # itself, where relative /api is correct and simpler.
    [string] $ApiBaseUrl = 'https://copsapi.sivayaantechnologies.com',

    [switch] $SelfContained,
    [switch] $SkipTests,
    [switch] $AllowDirty
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')

# npm and dotnet write ordinary progress and notices to stderr. Windows PowerShell wraps any
# stderr line from a native command in a NativeCommandError, and with $ErrorActionPreference =
# 'Stop' that terminates the script - so a build fails on "npm notice" while the command itself
# succeeded. Run native commands with the preference relaxed and judge them by their exit code,
# which is the only thing that actually reports failure.
function Invoke-Native {
    param([Parameter(Mandatory)] [string] $What, [Parameter(Mandatory)] [scriptblock] $Command)
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Command 2>&1 | Out-Host } finally { $ErrorActionPreference = $previous }
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit $LASTEXITCODE)." }
}
Push-Location $repo
try {
    # ---- 1. what exactly are we shipping ----
    $commit = (git rev-parse --short HEAD).Trim()
    $branch = (git rev-parse --abbrev-ref HEAD).Trim()
    $dirty  = [bool](git status --porcelain)
    if ($dirty -and -not $AllowDirty) {
        throw "The working tree has uncommitted changes. Commit them, or pass -AllowDirty for a test build."
    }
    $version = "{0}-{1}{2}" -f (Get-Date -Format 'yyyy.MM.dd'), $commit, $(if ($dirty) { '-dirty' } else { '' })
    Write-Host "Building $version  (branch $branch)" -ForegroundColor Cyan

    # ---- 2. the tests are the gate ----
    if (-not $SkipTests) {
        Write-Host "`n== dotnet test ==" -ForegroundColor Cyan
        Invoke-Native 'dotnet test' { dotnet test --configuration $Configuration --nologo }
    }

    # ---- 3. the UI, built twice ----
    # Twice, because the two copies answer different questions. The one inside wwwroot uses relative
    # /api and exists so the API alone serves a working site for a smoke test. The one for Cloudflare
    # is compiled against the API's absolute origin, since on the edge there is no /api to be
    # relative to. Same source, same commit; only the baked-in address differs.
    Write-Host "`n== building the web client ==" -ForegroundColor Cyan
    $wwwroot     = Join-Path $repo 'src\Swarnakshi.Api\wwwroot'
    $frontendOut = Join-Path $OutputRoot 'frontend'
    if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }
    if (Test-Path $OutputRoot) { Remove-Item $OutputRoot -Recurse -Force }

    Push-Location (Join-Path $repo 'web')
    try {
        # `npm ci` installs exactly what package-lock.json pins, so a deploy build cannot pick up a
        # different dependency version than the one that was tested.
        if (Test-Path 'package-lock.json') {
            Invoke-Native 'npm ci' { npm ci }
        } else {
            Invoke-Native 'npm install' { npm install }
        }

        # (a) same-origin, for the API's own wwwroot
        $env:VITE_API_BASE_URL = ''
        Invoke-Native 'the web build' { npm run build -- --outDir $wwwroot --emptyOutDir }

        # (b) pointed at the API's public origin, for Cloudflare Pages
        $env:VITE_API_BASE_URL = $ApiBaseUrl
        Write-Host "    Second build calls: $(if ($ApiBaseUrl) { $ApiBaseUrl } else { '(same origin)' })"
        Invoke-Native 'the frontend build' { npm run build -- --outDir $frontendOut --emptyOutDir }
        $env:VITE_API_BASE_URL = ''
    } finally { Pop-Location }

    if (-not (Test-Path (Join-Path $wwwroot 'index.html'))) {
        throw "The web build produced no index.html at $wwwroot."
    }
    if (-not (Test-Path (Join-Path $frontendOut 'index.html'))) {
        throw "The frontend build produced no index.html at $frontendOut."
    }

    # Cloudflare Pages serves files, and every deep link (/projects/<id>) is a path with no file
    # behind it. Without this it answers 404 and the app looks broken on refresh; with it, the
    # shell is returned and React Router resolves the route. 200 rather than a redirect, so the
    # address bar keeps the URL the user actually asked for.
    "/*    /index.html   200" | Out-File (Join-Path $frontendOut '_redirects') -Encoding ascii

    # Hashed assets are immutable and may be cached forever; index.html must not be, or a browser
    # keeps loading an old shell that asks for asset files the new deploy has already removed.
    @"
/assets/*
  Cache-Control: public, max-age=31536000, immutable

/index.html
  Cache-Control: no-cache
"@ | Out-File (Join-Path $frontendOut '_headers') -Encoding ascii

    # ---- 4. the API, with the UI inside it ----
    Write-Host "`n== publishing the API ==" -ForegroundColor Cyan
    $appOut = Join-Path $OutputRoot 'app'
    New-Item -ItemType Directory -Force -Path $appOut | Out-Null

    $publishArgs = @(
        'publish', (Join-Path $repo 'src\Swarnakshi.Api\Swarnakshi.Api.csproj')
        '--configuration', $Configuration
        '--output', $appOut
        '--nologo'
        "-p:InformationalVersion=$version"
    )
    if ($SelfContained) {
        # Bundles the runtime. Bigger package, but the server needs nothing installed beyond Windows.
        $publishArgs += @('--runtime', 'win-x64', '--self-contained', 'true')
    } else {
        $publishArgs += @('--runtime', 'win-x64', '--self-contained', 'false')
    }
    Invoke-Native 'dotnet publish' { dotnet @publishArgs }

    # A Development settings file must never travel to a server -- it would turn on the demo seeder
    # and the interactive API docs if the environment variable were ever missing.
    Remove-Item (Join-Path $appOut 'appsettings.Development.json') -ErrorAction SilentlyContinue

    # ---- 5. the scripts, SQL and settings template the server needs, beside the binaries ----
    # Regenerate the schema script from the migrations that just compiled, so a package can never
    # ship binaries expecting one schema next to a script that builds another.
    Write-Host "`n== regenerating the schema script ==" -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'New-SchemaScript.ps1') | Out-Host

    Copy-Item (Join-Path $PSScriptRoot '..\sql')     (Join-Path $OutputRoot 'sql')     -Recurse
    Copy-Item (Join-Path $PSScriptRoot '..\scripts') (Join-Path $OutputRoot 'scripts') -Recurse
    # Deploy.ps1 points the operator at this when there is no settings file yet, so it has to
    # travel in the package rather than only existing back in the repository.
    Copy-Item (Join-Path $PSScriptRoot '..\appsettings.Production.template.json') $OutputRoot

    [ordered]@{
        Version = $version; Commit = $commit; Branch = $branch
        BuiltAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        BuiltBy = "$env:USERNAME@$env:COMPUTERNAME"
        SelfContained = [bool]$SelfContained
    } | ConvertTo-Json | Out-File (Join-Path $OutputRoot 'build.json') -Encoding utf8

    $mb = { param($path) '{0:N1} MB' -f ((Get-ChildItem $path -Recurse -File | Measure-Object Length -Sum).Sum / 1MB) }
    Write-Host "`nBuilt $version" -ForegroundColor Green
    Write-Host "  $OutputRoot"
    Write-Host "    frontend\   $(& $mb $frontendOut)   -> upload to Cloudflare Pages"
    Write-Host "    app\        $(& $mb $appOut)   -> copy into the IIS site"
    Write-Host "    sql\        run 01-create-database.sql then 03-schema.sql on the SQL Server"
    Write-Host "    scripts\    Deploy.ps1 if you want the Windows-service install instead of IIS"
    Write-Host "`n  The UI in frontend\ calls: $(if ($ApiBaseUrl) { $ApiBaseUrl } else { 'its own origin' })"
    Write-Host "  That origin must appear in the API's Cors:Origins, or the browser will block it."
} finally { Pop-Location }
