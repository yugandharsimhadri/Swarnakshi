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
$svc = (Get-CimInstance Win32_Service -Filter "Name='MSSQL`$SQLEXPRESS'" -ErrorAction SilentlyContinue).StartName
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

# CHECKSUM + VERIFYONLY below: a backup nobody has verified is a guess. COMPRESSION is available
# on Express since 2022; if the server is older this line is the one to drop.
$sql = @"
BACKUP DATABASE [$Database] TO DISK = N'$file'
WITH INIT, COMPRESSION, CHECKSUM, STATS = 25,
     NAME = N'$Database full backup ($Label)';
RESTORE VERIFYONLY FROM DISK = N'$file' WITH CHECKSUM;
"@

Write-Host "Backing up $Database to $file"
sqlcmd -S $Server -E -C -b -Q $sql
if ($LASTEXITCODE -ne 0) { throw "Backup failed. The deployment must not continue." }

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
