<#
.SYNOPSIS
    Takes a compressed full backup of the SCOPS database and prunes old ones.

.DESCRIPTION
    Deploy.ps1 calls this before every deployment, so there is always a restore point taken
    minutes before the change that might need undoing. Schedule it nightly as well -- a backup
    that only exists on deployment days is not a backup.

    Backups are written by the SQL Server service account, so BackupPath must be a folder that
    account can write to. It is created and permissioned on first run.

.EXAMPLE
    .\Backup-Database.ps1
    .\Backup-Database.ps1 -Label 'before-v2' -KeepDays 90
#>
[CmdletBinding()]
param(
    [string] $Server     = '.\SQLEXPRESS',
    [string] $Database   = 'SCOPS',
    [string] $BackupPath = 'C:\Swarnakshi\backups',
    [string] $Label      = 'scheduled',
    [int]    $KeepDays   = 30
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $BackupPath)) { New-Item -ItemType Directory -Force -Path $BackupPath | Out-Null }

# The backup is written by the SQL Server service, not by this script's user, so the service
# account needs to be able to write here. Without this the BACKUP fails with "Operating system
# error 5 (Access is denied)" and the cause is not obvious from the message.
#
# Which service that is depends on the instance: MSSQLSERVER for a default instance, MSSQL$NAME
# for a named one. Derived from -Server rather than assumed, and skipped entirely when SQL is on
# another host - there the backup path is a path on *that* machine and this cannot help.
$instance = if ($Server -match '\\(.+)$') { "MSSQL`$$($Matches[1])" } else { 'MSSQLSERVER' }
$isLocal  = $Server -match '^(\.|\(local\)|localhost|127\.0\.0\.1)(\\|$)' -or
            $Server -match "^$([regex]::Escape($env:COMPUTERNAME))(\\|$)"
$svc = $null
if ($isLocal) {
    $svc = (Get-CimInstance Win32_Service -Filter "Name='$instance'" -ErrorAction SilentlyContinue).StartName
}
if ($svc) {
    try {
        $acl = Get-Acl $BackupPath
        $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule(
            $svc, 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
        Set-Acl $BackupPath $acl
    } catch { Write-Warning "Could not grant $svc write access to $BackupPath : $($_.Exception.Message)" }
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$file  = Join-Path $BackupPath "$Database-$stamp-$Label.bak"

# Express does not support backup compression - BACKUP fails outright with "WITH COMPRESSION is not
# supported on Express Edition", it does not warn and carry on. Ask the server what it is rather
# than assuming, so this works on Express and still compresses where compression is available.
$edition = (& sqlcmd -S $Server -E -C -h-1 -W -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('EngineEdition') AS int);" 2>&1 |
            Where-Object { $_ -match '^\s*\d+\s*$' } | Select-Object -First 1)
$compress = if ("$edition".Trim() -eq '4') { '' } else { ', COMPRESSION' }   # 4 = Express
if (-not $compress) { Write-Host "Express Edition: writing an uncompressed backup." }

# CHECKSUM and the VERIFYONLY that follows: a backup nobody has verified is a guess.
$sql = @"
BACKUP DATABASE [$Database] TO DISK = N'$file'
WITH INIT$compress, CHECKSUM, STATS = 25,
     NAME = N'$Database full backup ($Label)';
RESTORE VERIFYONLY FROM DISK = N'$file' WITH CHECKSUM;
"@

Write-Host "Backing up $Database to $file"
$previous = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try { $output = & sqlcmd -S $Server -E -C -b -Q $sql 2>&1 } finally { $ErrorActionPreference = $previous }
$output | Out-Host

if ($LASTEXITCODE -ne 0) {
    # BACKUP writes as the SQL Server service, not as whoever ran this, and the two see the world
    # differently. Worth spelling out, because the raw message says "cannot open backup device" and
    # leaves the reader to work out whose problem it is.
    $detail = ($output | Out-String)
    $hint =
        if ($detail -match 'operating system error 5')      { "The SQL Server service account cannot write to $BackupPath. Grant it Modify there." }
        elseif ($detail -match 'operating system error 3|error 2\(')  { "The SQL Server service cannot see $BackupPath. A mapped or SUBST drive is invisible to a service - use a real local path, or a UNC path the service account can reach." }
        elseif ($detail -match 'COMPRESSION is not supported') { "This edition does not support backup compression; the script should have detected that. Report it." }
        else { "Run the BACKUP statement by hand in SSMS to see the full error." }

    throw "Backup failed, so the deployment must not continue. $hint"
}

$mb = '{0:N1} MB' -f ((Get-Item $file).Length / 1MB)
Write-Host "Backup verified: $file ($mb)" -ForegroundColor Green

# Prune, but never leave the folder empty -- if every backup is older than KeepDays, the newest stays.
$old = Get-ChildItem $BackupPath -Filter "$Database-*.bak" |
       Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } |
       Sort-Object LastWriteTime
$keepNewest = (Get-ChildItem $BackupPath -Filter "$Database-*.bak" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
foreach ($f in $old) {
    if ($f.FullName -ne $keepNewest) { Remove-Item $f.FullName -Force; Write-Host "Pruned $($f.Name)" }
}

$file
