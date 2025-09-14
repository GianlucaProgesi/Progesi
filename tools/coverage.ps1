param(
  [switch]$RunTests,
  [int]$MinLine = 0
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

# Progetti e file
$projCore   = Join-Path $root 'tests/ProgesiCore.Tests/ProgesiCore.Tests.csproj'
$projSqlite = Join-Path $root 'tests/ProgesiRepositories.Sqlite.Tests/ProgesiRepositories.Sqlite.Tests.csproj'

$covCore    = Join-Path $root 'tests/ProgesiCore.Tests/TestResults/Coverage/coverage.cobertura.xml'
$covSqlite  = Join-Path $root 'tests/ProgesiRepositories.Sqlite.Tests/TestResults/Coverage/coverage.cobertura.xml'

$mergedDir  = Join-Path $root 'TestResults/MergedCoverage'
$mergedCov  = Join-Path $mergedDir 'Cobertura.xml'
$summaryTxt = Join-Path $mergedDir 'Summary.txt'

function Clean-Results {
  @(
    (Join-Path $root 'tests/ProgesiCore.Tests/TestResults'),
    (Join-Path $root 'tests/ProgesiRepositories.Sqlite.Tests/TestResults'),
    $mergedDir
  ) | ForEach-Object {
    if (Test-Path $_) { Remove-Item $_ -Recurse -Force -ErrorAction SilentlyContinue }
  }
}

function Run-Coverage {
  $exclude = '**/*LegacyExtensions.cs;**/Logging*.cs;**/Persistence/*.cs'
  $argsCommon = @(
    '/p:CollectCoverage=true',
    '/p:CoverletOutput=TestResults/Coverage/',
    '/p:CoverletOutputFormat=cobertura',
    # IMPORTANTE: virgolette propagate fino a MSBuild
    "/p:ExcludeByFile=""$exclude"""
  )

  Write-Host ">> dotnet test ($projCore) con coverlet (Cobertura)..." -ForegroundColor Cyan
  dotnet test $projCore -c Debug @argsCommon

  Write-Host ">> dotnet test ($projSqlite) con coverlet (Cobertura)..." -ForegroundColor Cyan
  dotnet test $projSqlite -c Debug @argsCommon
}

function Merge-Reports {
  if (!(Test-Path $covCore) -or !(Test-Path $covSqlite)) {
    throw "Cobertura non trovati. Core: '$covCore' esiste: $(Test-Path $covCore); Sqlite: '$covSqlite' esiste: $(Test-Path $covSqlite)"
  }
  New-Item -ItemType Directory -Force -Path $mergedDir | Out-Null
  Write-Host ">> Merge coverage con ReportGenerator..." -ForegroundColor Yellow
  $reports = "$covCore;$covSqlite"
  reportgenerator "-reports:$reports" "-targetdir:$mergedDir" "-reporttypes:Cobertura;TextSummary" | Out-Host
}

function Print-And-Gate {
  # Se il merge non c'è ma i 2 Cobertura sì, effettua ora il merge
  if (!(Test-Path $mergedCov) -and (Test-Path $covCore) -and (Test-Path $covSqlite)) {
    Merge-Reports
  }
  if (!(Test-Path $mergedCov)) { throw "Report merged non trovato: $mergedCov" }

  [xml]$xml = Get-Content $mergedCov
  $valid   = [int]$xml.coverage.'lines-valid'
  $covered = [int]$xml.coverage.'lines-covered'
  if ($valid -le 0) { throw "Cobertura senza metriche valide." }
  $pct = [math]::Round(($covered / $valid) * 100, 2)

  Write-Host ""
  Write-Host "### Test coverage" -ForegroundColor Green
  Write-Host "Report: $mergedCov"
  Write-Host ""
  Write-Host "| Metric        | Value |"
  Write-Host "|---            | ---:  |"
  Write-Host ("| Lines covered | {0} |" -f $covered)
  Write-Host ("| Lines valid   | {0}   |" -f $valid)
  Write-Host ("| **Line %**    | **{0}%** |" -f $pct)

  if ($MinLine -gt 0 -and $pct -lt $MinLine) {
    throw ("Gate FALLITO: coverage {0}% < soglia {1}%." -f $pct, $MinLine)
  }
}

# ---------------- main ----------------
if ($RunTests) {
  Clean-Results
  Run-Coverage
  Merge-Reports
  Print-And-Gate
  exit 0
}

if ($MinLine -gt 0) {
  # In CI abbiamo già eseguito i test; qui facciamo (se serve) solo il merge e il gate
  Print-And-Gate
  exit 0
}

if (Test-Path $mergedCov) { Print-And-Gate } else { Write-Host "Nessun coverage merge trovato. Usa -RunTests." }
