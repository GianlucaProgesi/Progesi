# tools\Backup-Progesi.ps1
param(
  [string]$BackupRemoteUrl = ""  # es. "https://github.com/<org>/<Progesi-backup>.git"
)

$ErrorActionPreference = "Stop"
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw "Git non è nel PATH." }
if (-not (Test-Path ".git")) { throw "Questa cartella non è un repository Git." }

$stamp   = (Get-Date).ToString("yyyyMMdd-HHmmss")
$backups = ".backups"
New-Item -ItemType Directory -Force -Path $backups | Out-Null
$tagName = "backup-$stamp"
$bundle  = Join-Path $backups "Progesi-$stamp.bundle"
$srcZip  = Join-Path $backups "Progesi-src-$stamp.zip"

Write-Host "==> Tag $tagName" ; git tag -a $tagName -m "Automated backup $stamp" 2>$null
Write-Host "==> Bundle $bundle" ; git bundle create $bundle --all --tags
Write-Host "==> Zip $srcZip"     ; git archive --format=zip -o $srcZip HEAD

if ($BackupRemoteUrl) {
  $remoteName = "backup"
  $remotes = (git remote) -split '\r?\n'
  if ($remotes -notcontains $remoteName) { git remote add $remoteName $BackupRemoteUrl } else { git remote set-url $remoteName $BackupRemoteUrl }
  Write-Host "==> Push mirror su '$remoteName'"
  git push --mirror $remoteName
  git push $remoteName --tags
}

Write-Host "`nBackup completato:"
Write-Host " - Bundle: $bundle"
Write-Host " - Sorgenti: $srcZip"
Write-Host " - Tag: $tagName"
if ($BackupRemoteUrl) { Write-Host " - Remote di backup: $BackupRemoteUrl" } else { Write-Host " - Nessun push remoto eseguito." }
