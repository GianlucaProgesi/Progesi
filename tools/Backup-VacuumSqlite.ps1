<# Backup-VacuumSqlite.ps1
   Crea una copia del DB e fa VACUUM sulla copia.

USO:
  .\tools\Backup-VacuumSqlite.ps1 -DatabasePath ".\tests\P6-live\progesi_p6.db" `
                                  -OutPath ".\backups\progesi_$(Get-Date -Fo yyyyMMdd_HHmmss).db"
#>

[CmdletBinding()] param(
  [Parameter(Mandatory=$true)] [string]$DatabasePath,
  [Parameter(Mandatory=$false)] [string]$OutPath = (Join-Path ".\backups" ("progesi_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".db"))
)

$ErrorActionPreference='Stop'

$sqlite = Join-Path $PSScriptRoot 'sqlite3\sqlite3.exe'
if (!(Test-Path $sqlite)) {
  $cmd = Get-Command sqlite3 -ErrorAction SilentlyContinue
  if ($cmd) { $sqlite = $cmd.Source }
}
if (-not (Test-Path $sqlite)) { throw "sqlite3 non trovato. Metti sqlite3.exe in tools\sqlite3 oppure nel PATH." }

$db = (Resolve-Path $DatabasePath).Path
$out= (Resolve-Path (Split-Path $OutPath -Parent) -ErrorAction SilentlyContinue)
if (-not $out) { New-Item -ItemType Directory -Force -Path (Split-Path $OutPath -Parent) | Out-Null }

Copy-Item -Force $db $OutPath
& $sqlite $OutPath "VACUUM;"

Write-Host "BACKUP + VACUUM → $OutPath" -ForegroundColor Green
