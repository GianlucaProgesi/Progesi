[CmdletBinding()]
param(
  [string]$Ref = "main",
  [int]$TimeoutMinutes = 20,
  [switch]$NoTrigger  # se vuoi solo leggere lo stato corrente senza rilanciare i workflow
)

$ErrorActionPreference = "Stop"

function Start-And-WaitWorkflow {
  param([string]$WorkflowName, [string]$Ref)
  if (-not $NoTrigger) {
    Write-Host "â†’ Trigger '$WorkflowName' on '$Ref'..." -ForegroundColor Cyan
    gh workflow run "$WorkflowName" --ref "$Ref" | Out-Null
  } else {
    Write-Host "â†’ Skip trigger for '$WorkflowName' (NoTrigger set)" -ForegroundColor DarkYellow
  }
  $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
  do {
    Start-Sleep -Seconds 5
    $run = gh run list --workflow "$WorkflowName" --branch "$Ref" --limit 1 --json databaseId,status,conclusion,headSha,createdAt -q ".[0]" 2>$null
  } until ( ($run -and $run.status -eq "completed") -or (Get-Date) -ge $deadline )

  if (-not $run)               { throw "Nessun run trovato per '$WorkflowName' su '$Ref'." }
  if ($run.status -ne "completed") { throw "'$WorkflowName' non completato entro $TimeoutMinutes min." }
  return $run
}

function Download-Artifacts {
  param([int]$RunId, [string]$TargetDir, [string[]]$Names)
  foreach ($n in $Names) {
    try { gh run download $RunId -n $n -D $TargetDir | Out-Null } catch {}
  }
}

function Read-Coverage {
  param([string]$BaseDir)
  $reports = Get-ChildItem -Recurse -Path $BaseDir -Filter coverage.cobertura.xml -ErrorAction SilentlyContinue
  if (-not $reports) { return $null }
  $sumCovered = 0.0; $sumValid = 0.0
  foreach ($r in $reports) {
    try {
      $xml = [xml](Get-Content -Raw $r.FullName)
      $covered = [double]($xml.coverage.'lines-covered')
      $valid   = [double]($xml.coverage.'lines-valid')
      if ($valid -gt 0) {
        $sumCovered += $covered
        $sumValid   += $valid
      } else {
        # fallback: usa line-rate (0..1)
        $rate = [double]($xml.coverage.'line-rate')
        $sumCovered += $rate
        $sumValid   += 1
      }
    } catch {}
  }
  if ($sumValid -le 0) { return $null }
  [math]::Round(100.0 * ($sumCovered / $sumValid), 2)
}

Write-Host "== Progesi :: Repo Health Check ==" -ForegroundColor Green
Write-Host "Ref: $Ref    Timeout: $TimeoutMinutes min" -ForegroundColor Gray

# 1) Lancia/attendi CI
$ciRun = Start-And-WaitWorkflow -WorkflowName "CI" -Ref $Ref
Write-Host ("CI: {0}" -f $ciRun.conclusion) -ForegroundColor (if ($ciRun.conclusion -eq "success"){"Green"}else{"Red"})
Download-Artifacts -RunId $ciRun.databaseId -TargetDir "out\healthcheck\CI" -Names @("coverage-html","test-results")

# 2) Lancia/attendi CodeQL
$qlRun = Start-And-WaitWorkflow -WorkflowName "CodeQL" -Ref $Ref
Write-Host ("CodeQL: {0}" -f $qlRun.conclusion) -ForegroundColor (if ($qlRun.conclusion -eq "success"){"Green"}else{"Red"})
Download-Artifacts -RunId $qlRun.databaseId -TargetDir "out\healthcheck\CodeQL" -Names @()  # niente artifact standard

# 3) Coverage (se presente)
$cov = Read-Coverage -BaseDir "out\healthcheck\CI"
if ($cov -ne $null) {
  Write-Host ("Coverage stimata: {0} %" -f $cov) -ForegroundColor Cyan
} else {
  Write-Host "Coverage: nessun Cobertura trovato in questo run (ok, CI resta verde comunque)." -ForegroundColor DarkYellow
}

# 4) Check igienici
Write-Host "`n== Hygiene checks ==" -ForegroundColor Green
$wf = (Get-ChildItem '.github/workflows' -File | Select-Object -ExpandProperty Name)
Write-Host "Workflow files:" $($wf -join ', ')
$extra = $wf | Where-Object { $_ -notin @('ci.yml','codeql.yml','release.yml','pr-labeler.yml','semantic-pr.yml') }
if ($extra) { Write-Host "âš ï¸ Extra workflow presenti: $($extra -join ', ')" -ForegroundColor Yellow } else { Write-Host "âœ… Solo i 5 workflow canonici." -ForegroundColor Green }

Write-Host "`nOpen PR:" -ForegroundColor Gray
try {
  gh pr list -s open --json number,title,headRefName -q '.[] | "#\(.number)  \(.title)  (\(.headRefName))"'
} catch { Write-Host "(gh non disponibile o nessuna PR aperta)" }

Write-Host "`nArtifacts scaricati in: out\healthcheck\CI  (coverage-html, test-results)" -ForegroundColor Gray
Write-Host "== DONE ==" -ForegroundColor Green

