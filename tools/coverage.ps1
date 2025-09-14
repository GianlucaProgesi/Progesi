<# 
  tools/coverage.ps1

  Uso:
    # Esegue i test e produce i report per ciascun test project
    pwsh ./tools/coverage.ps1 -RunTests

    # Applica il gate leggendo il report (o il merge) più recente
    pwsh ./tools/coverage.ps1 -MinLine 80

    # Tutto insieme
    pwsh ./tools/coverage.ps1 -RunTests -MinLine 80
#>

[CmdletBinding()]
param(
  [switch]$RunTests,
  [int]$MinLine = -1,
  [string]$Configuration = "Debug"
)

$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
  $root = (& git rev-parse --show-toplevel 2>$null)
  if (-not $root) { $root = (Resolve-Path .).Path }
  return $root
}

function Invoke-Tests {
  param([string]$Root)
  Write-Host ">> dotnet test con coverlet (Cobertura)..." -ForegroundColor Cyan

  dotnet test $Root `
    --nologo -m:1 -c $Configuration `
    -p:CollectCoverage=true `
    -p:CoverletOutputFormat=cobertura `
    -p:DeterministicReport=true `
    -p:CoverletOutput="TestResults/Coverage/" | Write-Host
}

function Find-CoberturaFiles {
  param([string]$Root)
  $patterns = @('coverage.cobertura.xml','*cobertura*.xml')

  $collected = @(
    foreach ($p in $patterns) {
      Get-ChildItem -Path (Join-Path $Root '*') -Recurse -File -Filter $p -ErrorAction SilentlyContinue
    }
  )
  $files = $collected | Sort-Object LastWriteTime -Descending -Unique
  return $files
}

function Read-CoberturaSummary {
  param([string]$Path)
  [xml]$x = Get-Content -LiteralPath $Path

  # Cobertura root: <coverage lines-covered=".." lines-valid="..">
  $covered = [int]$x.coverage.'lines-covered'
  $valid   = [int]$x.coverage.'lines-valid'
  $pct     = if ($valid -gt 0) { [math]::Round(($covered/$valid)*100, 2) } else { 0 }

  [pscustomobject]@{
    File    = $Path
    Covered = $covered
    Valid   = $valid
    LinePct = $pct
  }
}

function Try-Merge-WithReportGenerator {
  param(
    [System.IO.FileInfo[]]$Reports,
    [string]$Root
  )

  $rg = Get-Command reportgenerator -ErrorAction SilentlyContinue
  if (-not $rg) { return $null }  # tool non disponibile → nessun merge

  $mergeDir = Join-Path $Root "TestResults/MergedCoverage"
  New-Item -ItemType Directory -Force -Path $mergeDir | Out-Null

  $reportsArg = ($Reports | ForEach-Object { $_.FullName }) -join ';'
  Write-Host ">> Merge coverage con ReportGenerator..." -ForegroundColor Cyan
  reportgenerator `
    "-reports:$reportsArg" `
    "-targetdir:$mergeDir" `
    "-reporttypes:Cobertura;TextSummary" `
    | Write-Host

  $mergedCobertura = Join-Path $mergeDir "Cobertura.xml"
  if (Test-Path $mergedCobertura) { return (Get-Item $mergedCobertura) }
  return $null
}

# --- MAIN ---
$root = Get-RepoRoot

if ($RunTests) {
  Invoke-Tests -Root $root
}

$reports = Find-CoberturaFiles -Root $root
if (-not $reports -or $reports.Count -eq 0) {
  throw "Nessun report Cobertura trovato (cercati sotto '$root')."
}

# Prova a fare il merge se ci sono più report e ReportGenerator è installato
$chosen = $reports[0]
if ($reports.Count -gt 1) {
  $merged = Try-Merge-WithReportGenerator -Reports $reports -Root $root
  if ($merged) { $chosen = $merged }
}

$sum = Read-CoberturaSummary -Path $chosen.FullName

# Costruisco la tabella Markdown
$md = @"
### Test coverage
Report: $($chosen.FullName)

| Metric        | Value |
|---            | ---:  |
| Lines covered | $($sum.Covered) |
| Lines valid   | $($sum.Valid)   |
| **Line %**    | **$($sum.LinePct)%** |
"@

# Se ho più report e NON ho fatto il merge, aggiungo nota e la lista
if ($reports.Count -gt 1 -and -not ($chosen.FullName -like "*MergedCoverage*")) {
  $md += "`n> Nota: trovati $($reports.Count) report; mostrato il più recente (merge non eseguito perché 'reportgenerator' non è nel PATH)."
  $md += "`n`n**Reports trovati (per timestamp):**`n"
  $md += ($reports | Select-Object FullName, LastWriteTime | Format-Table -AutoSize | Out-String)
}

Write-Host ""
Write-Host $md

if ($env:GITHUB_STEP_SUMMARY) {
  $md | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
}

if ($MinLine -ge 0) {
  if ($sum.LinePct -lt $MinLine) {
    Write-Error ("Gate FALLITO: coverage {0}% < soglia {1}%." -f $sum.LinePct, $MinLine)
    exit 1
  } else {
    Write-Host ("Gate OK: {0}% ≥ {1}%." -f $sum.LinePct, $MinLine) -ForegroundColor Green
  }
}
