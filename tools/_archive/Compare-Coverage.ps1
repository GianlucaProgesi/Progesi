<#
.SYNOPSIS
  Stampa percentuali di coverage correnti (line/branch/method) e le confronta con una baseline.
#>

param(
  [string]$BaselinePath = "tests/coverage-baseline.json",
  [switch]$WarnOnly,
  [switch]$Strict,
  [double]$Tolerance = 0.05
)

function Parse-Summary {
  param([string]$Path)
  if (-not (Test-Path $Path)) { throw "Summary non trovato: $Path" }
  $text = Get-Content -Path $Path -Raw
  $result = [ordered]@{ line=$null; branch=$null; method=$null }
  if ($text -match 'Line coverage:\s*([0-9]+(?:\.[0-9]+)?)%')   { $result.line   = [double]$Matches[1] }
  if ($text -match 'Branch coverage:\s*([0-9]+(?:\.[0-9]+)?)%') { $result.branch = [double]$Matches[1] }
  if ($text -match 'Method coverage:\s*([0-9]+(?:\.[0-9]+)?)%') { $result.method = [double]$Matches[1] }
  return $result
}

function Load-Baseline {
  param([string]$Path)
  if (-not (Test-Path $Path)) { return $null }
  try { return Get-Content -Path $Path -Raw | ConvertFrom-Json } catch { return $null }
}

try {
  $summaryPath = "CoverageReport/Summary.txt"
  $curr = Parse-Summary -Path $summaryPath

  Write-Host "=== Coverage corrente ===" -ForegroundColor Yellow
  "{0,8} {1,8} {2,8}" -f "Line(%)","Branch(%)","Method(%)" | Write-Host
  "{0,8:N1} {1,8:N1} {2,8:N1}" -f ($curr.line ?? 0), ($curr.branch ?? 0), ($curr.method ?? 0) | Write-Host

  $out = [ordered]@{
    line   = $curr.line
    branch = $curr.branch
    method = $curr.method
    generated = (Get-Date).ToString("s")
  } | ConvertTo-Json -Depth 3
  New-Item -ItemType Directory -Force -Path "CoverageReport" | Out-Null
  $out | Set-Content -Path "CoverageReport/CoverageCurrent.json" -NoNewline -Encoding UTF8

  $base = Load-Baseline -Path $BaselinePath
  if ($null -eq $base) {
    Write-Host "Baseline non trovata ($BaselinePath). Nessun confronto eseguito." -ForegroundColor DarkYellow
    exit 0
  }

  Write-Host "`n=== Confronto vs baseline ($BaselinePath) ===" -ForegroundColor Yellow
  $dLine   = ($curr.line   ?? 0) - ([double]$base.line)
  $dBranch = ($curr.branch ?? 0) - ([double]$base.branch)
  $dMethod = ($curr.method ?? 0) - ([double]$base.method)

  "{0,-10} {1,8} {2,8} {3,10}" -f "Metric","Base(%)","Now(%)","Delta(pts)" | Write-Host
  "{0,-10} {1,8:N1} {2,8:N1} {3,10:N1}" -f "Line",   [double]$base.line,   ($curr.line   ?? 0), $dLine   | Write-Host
  "{0,-10} {1,8:N1} {2,8:N1} {3,10:N1}" -f "Branch", [double]$base.branch, ($curr.branch ?? 0), $dBranch | Write-Host
  "{0,-10} {1,8:N1} {2,8:N1} {3,10:N1}" -f "Method", [double]$base.method, ($curr.method ?? 0), $dMethod | Write-Host

  $regress = (($dLine -lt -$Tolerance) -or ($dBranch -lt -$Tolerance) -or ($dMethod -lt -$Tolerance))
  if ($regress) {
    $msg = "ATTENZIONE: regressione di coverage oltre la tolleranza ${Tolerance}."
    if ($Strict) { Write-Error $msg; exit 1 } else { Write-Warning $msg; exit 0 }
  } else {
    Write-Host "OK: nessuna regressione oltre la tolleranza." -ForegroundColor Green
    exit 0
  }
}
catch {
  Write-Error $_.Exception.Message
  exit 1
}
