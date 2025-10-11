Param(
  [string]$Repo = "GianlucaProgesi/Progesi",
  [string]$CommitMessage = "chore: recovery axis + sqlite line aligned; 104 tests green",
  [switch]$RunCI = $true
)

$ErrorActionPreference = "Stop"
function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }

# Stato e branch di default
$remoteInfo = git remote show origin | Out-String
$DefaultBranch = (($remoteInfo -split "`r?`n") | Where-Object { $_ -match 'HEAD branch:' }) -replace '.*HEAD branch:\s*',''
$DefaultBranch = $DefaultBranch.Trim(); if (-not $DefaultBranch) { $DefaultBranch = "main" }
$currentBranch = (git rev-parse --abbrev-ref HEAD | Out-String).Trim()

# Se sei su main, lavora in un branch chore/ci-*
$branch = $currentBranch
if ($currentBranch -eq $DefaultBranch) {
  $ts = Get-Date -Format "yyyyMMdd-HHmmss"
  $branch = "chore/ci-$ts"
  Info ("Creo branch: {0}" -f $branch)
  git checkout -b $branch
}

# Commit se ci sono modifiche
$changes = (git status --porcelain | Out-String).Trim()
if ($changes) {
  Info "Committo modifiche..."
  git add -A
  git commit -m $CommitMessage
} else {
  Warn "Nessuna modifica da committare (ok)."
}

# Push + PR
Info ("Push su origin {0}..." -f $branch)
git push -u origin $branch
Info ("Creo PR verso {0}..." -f $DefaultBranch)
$prUrl = gh pr create --repo $Repo --base $DefaultBranch --head $branch --title $CommitMessage --body "Auto-PR: stato consolidato (104 test verdi)."
Ok ("PR creata: {0}" -f $prUrl)

# Avvio CI (opzionale)
if ($RunCI) {
  Info ("Avvio CI su {0}..." -f $branch)
  gh workflow run CI --ref $branch 2>$null
  Ok "CI avviata (se non parte, partirà comunque per la PR)."
}
