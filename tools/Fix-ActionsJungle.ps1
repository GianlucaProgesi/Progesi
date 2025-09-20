[CmdletBinding()]
param(
  [switch]$DeleteBranches,   # se passato, elimina i branch remoti “infetti”
  [switch]$CloseDependabotPR # se passato, chiude PR di dependabot aperte
)

$ErrorActionPreference = 'Stop'

Write-Host "== Progesi :: Actions Jungle Fix ==" -ForegroundColor Cyan

# 1) Mantieni solo i 5 workflow canonici su main
$keep = @('ci.yml','codeql.yml','release.yml','semantic-pr.yml','pr-labeler.yml')
$wfDir = ".github/workflows"
if (-not (Test-Path $wfDir)) { throw "Cartella $wfDir non trovata" }

$toRemove = Get-ChildItem $wfDir -File | Where-Object { $_.Name -notin $keep }
if ($toRemove) {
  Write-Host "Rimuovo da main i workflow legacy:" -ForegroundColor Yellow
  $toRemove | ForEach-Object { Write-Host " - $($_.FullName)" }
  git rm --quiet ($toRemove.FullName) | Out-Null
  git commit -m "ci: prune legacy workflows (keep only CI/CodeQL/release/semantic-pr/pr-labeler)" | Out-Null
} else {
  Write-Host "Main è già pulito (solo i 5 canonici)." -ForegroundColor Green
}

# 2) Trova branch remoti che contengono i workflow legacy
git fetch --all --prune | Out-Null
$heads = (git ls-remote --heads origin) -split "`n" | ForEach-Object {
  ($_ -split "`t")[1] -replace '^refs/heads/',''
}

$legacyPatterns = @(
  '.github/workflows/labeler.yml',
  '.github/workflows/build-test-coverage.yml'
)

$branchesWithLegacy = @()
foreach ($b in $heads) {
  if ($b -eq 'main') { continue }
  $files = git ls-tree -r --name-only "origin/$b"
  if ($files) {
    foreach ($p in $legacyPatterns) {
      if ($files -contains $p) {
        $branchesWithLegacy += $b
        break
      }
    }
  }
}

if ($branchesWithLegacy) {
  Write-Host "Branch remoti che contengono workflow legacy:" -ForegroundColor Yellow
  $branchesWithLegacy | ForEach-Object { Write-Host " - $_" }
  if ($DeleteBranches) {
    foreach ($b in $branchesWithLegacy) {
      Write-Host "Elimino branch remoto origin/$b ..." -ForegroundColor Magenta
      git push origin --delete "$b"
    }
  } else {
    Write-Host "Esegui di nuovo con -DeleteBranches per eliminarli." -ForegroundColor DarkYellow
  }
} else {
  Write-Host "Nessun branch remoto con workflow legacy." -ForegroundColor Green
}

# 3) Chiudi PR Dependabot (opzionale)
if ($CloseDependabotPR) {
  Write-Host "Chiudo PR aperte di Dependabot..." -ForegroundColor Cyan
  $prs = gh pr list --state open --json number,author --jq '.[] | select(.author.login=="dependabot") | .number'
  if ($prs) {
    $prs | ForEach-Object {
      Write-Host " - chiudo PR #$_" -ForegroundColor Magenta
      gh pr close $_ --delete-branch
    }
  } else {
    Write-Host "Nessuna PR Dependabot aperta." -ForegroundColor Green
  }
}

Write-Host "== DONE ==" -ForegroundColor Cyan
