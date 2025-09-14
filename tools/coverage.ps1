[CmdletBinding()]
param(
  [switch]$RunTests,
  [int]$MinLine = 0
)

$ErrorActionPreference = "Stop"
$root = (Get-Location).Path

function Get-CoberturaFiles {
  param([string]$BasePath)

  $files = @()
  try { $files += Get-ChildItem -Path $BasePath -Recurse -File -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue } catch {}
  try { $files += Get-ChildItem -Path $BasePath -Recurse -File -Filter 'Cobertura.xml'         -ErrorAction SilentlyContinue } catch {}

  if (-not $files) {
    try {
      $files += Get-ChildItem -Path $BasePath -Recurse -File -Include *.xml -ErrorAction SilentlyContinue |
               Where-Object { $_.Name -match 'cobertura' }
    } catch {}
  }

  $files | Sort-Object FullName -Unique
}

function Merge-And-Summarize {
  $covFiles = Get-CoberturaFiles -BasePath $root
  if (-not $covFiles -or $covFiles.Count -eq 0) {
    throw "Nessun report Cobertura trovato (cercati sotto '$root')."
  }

  $reports   = ($covFiles | ForEach-Object { $_.FullName }) -join ';'
  $targetDir = Join-Path $root 'TestResults/MergedCoverage'
  New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

  reportgenerator "-reports:$reports" "-targetdir:$targetDir" "-reporttypes:Cobertura;TextSummary" | Out-Null

  $merged = Join-Path $targetDir 'Cobertura.xml'
  if (-not (Test-Path $merged)) { throw "Merge fallito: file unificato non trovato: $merged" }

  [xml]$xml = Get-Content -LiteralPath $merged
  $cov      = $xml.coverage

  $covered  = [int]$cov.'lines-covered'
  $valid    = [int]$cov.'lines-valid'
  $percent  = if ($valid -gt 0) { [math]::Round(($covered / $valid) * 100, 2) } else { 0 }

  $md = @"
### Test coverage
Report: $merged

| Metric        | Value |
|---            | ---:  |
| Lines covered | $covered |
| Lines valid   | $valid   |
| **Line %**    | **$percent%** |
"@

  if ($env:GITHUB_STEP_SUMMARY) {
    $md | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
  }
  Write-Host $md

  return $percent
}

if ($RunTests) {
  # Esegue i test con i flag Coverlet (utile in locale; nel runner lo fa già il workflow)
  $projects = Get-ChildItem -Path (Join-Path $root 'tests') -Recurse -File -Filter *.csproj
  foreach ($p in $projects) {
    $outDir = Join-Path $p.Directory.FullName 'TestResults/Coverage'
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    & dotnet test $p.FullName -c Debug `
      /p:CollectCoverage=true `
      /p:CoverletOutput="$outDir/" `
      /p:CoverletOutputFormat="cobertura"
  }

  # In modalità -RunTests non falliamo se non troviamo report: il gate ci penserà dopo.
  try { Merge-And-Summarize | Out-Null } catch { Write-Warning $_.Exception.Message }
  return
}

# Solo merge + summary + gate (usato dal workflow)
$linePercent = Merge-And-Summarize
if ($MinLine -gt 0) {
  if ($linePercent -lt $MinLine) {
    throw ("Gate FALLITO: coverage {0}% < soglia {1}%." -f $linePercent, $MinLine)
  } else {
    Write-Host ("Gate OK: coverage {0}% ≥ soglia {1}%." -f $linePercent, $MinLine)
  }
}
