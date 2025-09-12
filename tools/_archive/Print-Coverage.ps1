<#
.SYNOPSIS
  Trova e stampa la coverage (linee e branch) da un file Cobertura/Opencover.
#>

param(
  # Cartella radice da cui cercare (default: repo corrente)
  [string]$Root = "."
)

function Get-CoverageFile {
  param([string]$Base)
  # 1) Cobertura classico generato da coverlet.collector
  $cov = Get-ChildItem -Path $Base -Recurse -File -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($cov) { return $cov }
  # 2) Opencover (fallback)
  $cov = Get-ChildItem -Path $Base -Recurse -File -Filter "coverage.opencover.xml" -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($cov) { return $cov }
  return $null
}

try {
  $cov = Get-CoverageFile -Base $Root
  if (-not $cov) {
    Write-Error "Nessun file di coverage trovato. Percorsi tipici: tests/*/TestResults/<GUID>/coverage.cobertura.xml"
    exit 1
  }

  [xml]$c = Get-Content -Path $cov.FullName

  # Prova prima con i contatori Cobertura (lines-covered/valid, branches-covered/valid)
  $lc = [int]($c.coverage.'lines-covered')
  $lv = [int]($c.coverage.'lines-valid')
  $bc = [int]($c.coverage.'branches-covered')
  $bv = [int]($c.coverage.'branches-valid')

  # Calcola percentuali con contatori o, in fallback, con i rate (line-rate / branch-rate)
  if ($lv -gt 0) {
    $lp = [math]::Round(($lc / $lv) * 100, 2)
  } elseif ($c.coverage.'line-rate') {
    $lp = [math]::Round([double]$c.coverage.'line-rate' * 100, 2)
  } else {
    $lp = 0
  }

  if ($bv -gt 0) {
    $bp = [math]::Round(($bc / $bv) * 100, 2)
  } elseif ($c.coverage.'branch-rate') {
    $bp = [math]::Round([double]$c.coverage.'branch-rate' * 100, 2)
  } else {
    $bp = 0
  }

  # Output leggibile
  Write-Host ("File: {0}" -f $cov.FullName) -ForegroundColor Yellow
  Write-Host ("Lines   : {0} / {1} = {2}%" -f $lc, $lv, $lp)
  Write-Host ("Branches: {0} / {1} = {2}%" -f $bc, $bv, $bp)
}
catch {
  Write-Error $_.Exception.Message
  exit 1
}
