Param(
  [string]$Repo = "GianlucaProgesi/Progesi",
  [string]$CommitMessage = "feat(S2-B/C): CsvUtils for browsers + icons/tooltips; 104 tests green",
  [switch]$RunCI = $true
)
$ErrorActionPreference = "Stop"
function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw "git non trovato." }
if (-not (Get-Command gh  -ErrorAction SilentlyContinue)) { throw "GitHub CLI (gh) non trovato." }
gh auth status 1>$null 2>$null
if ($LASTEXITCODE -ne 0) { gh auth login -w | Out-Null }

# default branch
$remoteInfo = git remote show origin | Out-String
$DefaultBranch = (($remoteInfo -split "`r?`n") | Where-Object { $_ -match 'HEAD branch:' }) -replace '.*HEAD branch:\s*',''
$DefaultBranch = $DefaultBranch.Trim(); if (-not $DefaultBranch) { $DefaultBranch = "main" }

$current = (git rev-parse --abbrev-ref HEAD | Out-String).Trim()
# se sei su main, crea un branch chore
$branch = $current
if ($current -eq $DefaultBranch) {
  $ts = Get-Date -Format "yyyyMMdd-HHmmss"
  $branch = "chore/ci-$ts"
  Info ("Creo branch $branch")
  git checkout -b $branch | Out-Null
}

# commit se ci sono modifiche
$changes = (git status --porcelain | Out-String).Trim()
if ($changes) {
  Info "Commit..."
  git add -A
  git commit -m $CommitMessage | Out-Null
} else {
  Warn "Nessuna modifica da committare."
}

Info ("Push su origin/$branch")
git push -u origin $branch | Out-Null

# PR
Info ("Creo PR verso $DefaultBranch...")
try {
  $prUrl = gh pr create --repo $Repo --base $DefaultBranch --head $branch --title $CommitMessage --body "Auto-PR: S2-B/C"
  Ok ("PR: $prUrl")
} catch {
  Warn "PR forse già esistente. Visualizzo:"
  gh pr view --repo $Repo --head $branch
}

# Avvio CI (se possibile)
if ($RunCI) {
  Info "Avvio CI..."
  gh workflow run CI --ref $branch 2>$null
  Ok "CI avviata (o partirà dalla PR)."
}
Ok "Fatto."
