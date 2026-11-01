<#
.SYNOPSIS
    Runs the API (6051) and the UI (6050) for development, each in its own window.

.DESCRIPTION
    The UI proxies /api to the API, so open http://localhost:6050. Close a window to stop that half.
    Deliberately not 6061 or 6041: on the server those belong to IIS and the live site.
    The API takes its connection string from dotnet user-secrets.
#>
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')

Start-Process powershell -WorkingDirectory $repo -ArgumentList '-NoExit', '-Command',
    '$Host.UI.RawUI.WindowTitle = "Swarnakshi API :6051"; dotnet run --project src/Swarnakshi.Api --launch-profile http'

Start-Process powershell -WorkingDirectory (Join-Path $repo 'web') -ArgumentList '-NoExit', '-Command',
    '$Host.UI.RawUI.WindowTitle = "Swarnakshi UI :6050"; if (-not (Test-Path node_modules)) { npm install }; npm run dev'

Write-Host 'API  http://localhost:6051'
Write-Host 'UI   http://localhost:6050   <- open this one'
