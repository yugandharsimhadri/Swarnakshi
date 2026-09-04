<#
.SYNOPSIS
    Writes C:\Swarnakshi\app\appsettings.Production.json for a Swarnakshi server.

.DESCRIPTION
    A convenience for the first deployment. You do not have to use it - copying
    appsettings.Production.template.json to that path and editing it by hand does the same job,
    and after the first run editing the file directly is the normal way to change anything.

    What it adds over hand-editing is the signing key: it generates a real random one, and
    -KeepJwtKey preserves the existing key when you are only changing the connection string.
    That matters, because every access and refresh token is signed with it - replacing the key
    signs every user out.

    Deploy.ps1 never calls this except with -InitSettings, and never overwrites the file otherwise.

.EXAMPLE
    .\New-ProductionSettings.ps1 -ConnectionString 'Server=.\SQLEXPRESS;Database=SCOPS;User ID=SivayaanHMS;Password=...;TrustServerCertificate=True'

.EXAMPLE
    # Changing the password or the server, keeping everyone signed in:
    .\New-ProductionSettings.ps1 -ConnectionString '<the new one>' -KeepJwtKey
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ConnectionString,
    [string] $AppRoot   = 'C:\Swarnakshi',
    [string] $ListenUrl = 'http://localhost:6061',
    [string] $PlatformAdminPassword,          # omit to keep the application's built-in default
    [string[]] $CorsOrigins = @(),
    [switch] $KeepJwtKey,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
$target = Join-Path $AppRoot 'app\appsettings.Production.json'

if ((Test-Path $target) -and -not $Force -and -not $KeepJwtKey) {
    throw "$target already exists. Edit it directly, or re-run with -KeepJwtKey (changing the connection string) or -Force (replacing the file outright)."
}

# The signing key: reuse the live one unless there is none, because replacing it signs everyone out.
$jwtKey = $null
if ($KeepJwtKey -and (Test-Path $target)) {
    # Read it, and stop if we cannot. Falling through to "generate a new one" would sign every user
    # out as a side effect of a password change, which is exactly what -KeepJwtKey exists to avoid.
    try {
        $jwtKey = (Get-Content $target -Raw -ErrorAction Stop | ConvertFrom-Json).Jwt.Key
    } catch {
        throw ("-KeepJwtKey was given but $target could not be read: $($_.Exception.Message) " +
               "Run this from an elevated PowerShell. Generating a new key here would sign every " +
               "user out, so nothing was written.")
    }
    if ([string]::IsNullOrWhiteSpace($jwtKey)) {
        throw "-KeepJwtKey was given but $target holds no Jwt:Key to keep. Re-run with -Force to write a new one."
    }
    Write-Host "Keeping the existing JWT signing key -- sessions survive this change."
}
if (-not $jwtKey) {
    # RNGCryptoServiceProvider, not RandomNumberGenerator.Fill: Fill does not exist in Windows
    # PowerShell 5.1, which is what a Windows Server has out of the box.
    $bytes = New-Object byte[] 48
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $jwtKey = [Convert]::ToBase64String($bytes)          # 64 chars, well past the 32-char minimum
    Write-Host "Generated a new JWT signing key. Any existing session is now invalid."
}

$settings = [ordered]@{
    ConnectionStrings = [ordered]@{ Default = $ConnectionString }
    Database          = [ordered]@{ CommandTimeoutSeconds = 60 }
    Jwt               = [ordered]@{
        Issuer = 'Swarnakshi'; Audience = 'Swarnakshi'; Key = $jwtKey
        AccessTokenMinutes = 60; RefreshTokenDays = 7
    }
    Urls              = $ListenUrl
    # Empty on purpose: this service serves the UI itself, so a browser never makes a cross-origin
    # call. Add an origin here only if some other site must call this API.
    Cors              = [ordered]@{ Origins = $CorsOrigins }
    Seed              = [ordered]@{ Demo = $false }
    Storage           = [ordered]@{ LocalRoot = (Join-Path $AppRoot 'data\uploads') }
    Logging           = [ordered]@{ LogLevel = [ordered]@{
        Default = 'Information'; 'Microsoft.AspNetCore' = 'Warning'; 'Microsoft.EntityFrameworkCore' = 'Warning' } }
}

# Only write the operator's credentials when they were actually supplied. An absent section means
# PlatformSeedOptions keeps its own defaults, which is right; a section holding a placeholder would
# quietly become the EnterpriseAdmin password.
if ($PlatformAdminPassword) {
    $settings.Insert(2, 'PlatformAdmin',
        [ordered]@{ Username = 'EnterpriseAdmin'; Password = $PlatformAdminPassword })
}

New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
$settings | ConvertTo-Json -Depth 8 | Out-File -FilePath $target -Encoding utf8

# The file holds the database password, so it is readable by three principals and no one else:
# SYSTEM, because the service runs as LocalSystem; Administrators, to operate the box; and whoever
# ran this script, so a later -KeepJwtKey change can read the signing key back instead of failing.
$acl = Get-Acl $target
$acl.SetAccessRuleProtection($true, $false)   # stop inheriting the folder's broader rights
$principals = @(
    'NT AUTHORITY\SYSTEM'
    'BUILTIN\Administrators'
    [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
) | Select-Object -Unique
foreach ($who in $principals) {
    try {
        $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
            $who, 'FullControl', 'Allow')))
    } catch { Write-Warning "Could not grant $who access to the settings file: $($_.Exception.Message)" }
}
try {
    Set-Acl -Path $target -AclObject $acl
    Write-Host "Wrote $target"
    Write-Host "Readable by SYSTEM, Administrators and $env:USERNAME. It is git-ignored -- never commit it."
} catch {
    # Re-applying a protected ACL needs SeSecurityPrivilege, which an unelevated shell does not
    # hold. The file itself is already written, and on a rewrite it already carries these rights,
    # so this is a warning and not a failure -- but say so, because on a first run it means the
    # password is sitting under whatever the folder's rights happen to be.
    Write-Host "Wrote $target"
    Write-Warning ("Could not set permissions on it: $($_.Exception.Message)" +
                   " Re-run from an elevated PowerShell, or check the file's rights by hand.")
}
