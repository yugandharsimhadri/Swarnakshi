<#
.SYNOPSIS
    Builds the API and the UI and fails loudly if either does not compile. Add -Test to run the tests too.
    For a deployable package use deploy\scripts\Publish.ps1 instead.
#>
param([switch] $Test)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')

function Step($name, [scriptblock] $run) {
    Write-Host "`n== $name" -ForegroundColor Cyan
    & $run
    if ($LASTEXITCODE -ne 0) { throw "$name failed (exit $LASTEXITCODE)" }
}

Push-Location $repo
try {
    Step 'API: dotnet build' { dotnet build --nologo -v q }
    Push-Location web
    try {
        if (-not (Test-Path node_modules)) { Step 'UI: npm install' { npm install } }
        Step 'UI: npm run build' { npm run build }
    } finally { Pop-Location }
    if ($Test) { Step 'Tests: dotnet test' { dotnet test --nologo -v q --no-build } }
    Write-Host "`nBuild succeeded." -ForegroundColor Green
} finally { Pop-Location }
