<#
.SYNOPSIS
    Builds a deployable Swarnakshi package: the React UI, compiled into the API's wwwroot, and the
    API published for Windows.

.DESCRIPTION
    Produces one folder that contains everything the server needs. The UI is served by the API
    process out of wwwroot, so there is a single origin, a single port and a single thing to start.

    Run it on a build machine (or the server itself) from a clean checkout of the tag being shipped.
    It refuses to build from a dirty working tree unless -AllowDirty is given, so the version stamped
    into the package always names a commit that exists.

.EXAMPLE
    .\deploy\scripts\Publish.ps1
    .\deploy\scripts\Publish.ps1 -SelfContained     # bundles the .NET runtime; no runtime install needed
#>
[CmdletBinding()]
param(
    [string] $OutputRoot   = (Join-Path $PSScriptRoot '..\out'),
    [string] $Configuration = 'Release',
    [switch] $SelfContained,
    [switch] $SkipTests,
    [switch] $AllowDirty
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
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
        dotnet test --configuration $Configuration --nologo
        if ($LASTEXITCODE -ne 0) { throw "Tests failed. Nothing was published." }
    }

    # ---- 3. the UI, compiled into the API's wwwroot ----
    Write-Host "`n== building the web client ==" -ForegroundColor Cyan
    $wwwroot = Join-Path $repo 'src\Swarnakshi.Api\wwwroot'
    if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }

    Push-Location (Join-Path $repo 'web')
    try {
        # `npm ci` installs exactly what package-lock.json pins, so a deploy build cannot pick up a
        # different dependency version than the one that was tested.
        if (Test-Path 'package-lock.json') { npm ci } else { npm install }
        if ($LASTEXITCODE -ne 0) { throw "npm install failed." }
        npm run build -- --outDir $wwwroot --emptyOutDir
        if ($LASTEXITCODE -ne 0) { throw "The web build failed." }
    } finally { Pop-Location }

    if (-not (Test-Path (Join-Path $wwwroot 'index.html'))) {
        throw "The web build produced no index.html at $wwwroot."
    }

    # ---- 4. the API, with the UI inside it ----
    Write-Host "`n== publishing the API ==" -ForegroundColor Cyan
    $appOut = Join-Path $OutputRoot 'app'
    if (Test-Path $OutputRoot) { Remove-Item $OutputRoot -Recurse -Force }
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
    dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

    # A Development settings file must never travel to a server -- it would turn on the demo seeder
    # and the interactive API docs if the environment variable were ever missing.
    Remove-Item (Join-Path $appOut 'appsettings.Development.json') -ErrorAction SilentlyContinue

    # ---- 5. the scripts, SQL and settings template the server needs, beside the binaries ----
    # Regenerate the schema script from the migrations that just compiled, so a package can never
    # ship binaries expecting one schema next to a script that builds another.
    Write-Host "`n== regenerating the schema script ==" -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'New-SchemaScript.ps1') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Could not regenerate deploy\sql\03-schema.sql." }

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

    $size = '{0:N0} MB' -f ((Get-ChildItem $OutputRoot -Recurse -File | Measure-Object Length -Sum).Sum / 1MB)
    Write-Host "`nPackage ready: $OutputRoot  ($size)" -ForegroundColor Green
    Write-Host "Copy it to the server and run scripts\Deploy.ps1 from inside it."
} finally { Pop-Location }
